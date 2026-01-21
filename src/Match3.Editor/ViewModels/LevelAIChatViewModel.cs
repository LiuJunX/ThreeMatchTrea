using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using Match3.Editor.Interfaces;
using Match3.Editor.Logic;
using Match3.Editor.Models;

namespace Match3.Editor.ViewModels
{
    /// <summary>
    /// AI 对话式关卡编辑 ViewModel
    /// </summary>
    public class LevelAIChatViewModel : INotifyPropertyChanged, IDisposable
    {
        private readonly ILevelAIChatService _aiService;
        private readonly LevelEditorViewModel _editorViewModel;
        private readonly IntentExecutor _intentExecutor;
        private CancellationTokenSource? _cts;

        public ObservableCollection<ChatMessage> Messages { get; } = new ObservableCollection<ChatMessage>();

        private string _inputText = "";
        public string InputText
        {
            get => _inputText;
            set { _inputText = value; OnPropertyChanged(nameof(InputText)); }
        }

        private bool _isWaitingResponse;
        public bool IsWaitingResponse
        {
            get => _isWaitingResponse;
            private set { _isWaitingResponse = value; OnPropertyChanged(nameof(IsWaitingResponse)); }
        }

        private bool _isVisible = true;
        public bool IsVisible
        {
            get => _isVisible;
            set { _isVisible = value; OnPropertyChanged(nameof(IsVisible)); }
        }

        public bool IsAvailable => _aiService?.IsAvailable ?? false;

        public event PropertyChangedEventHandler? PropertyChanged;
        public event Action? OnMessagesChanged;
        public event Action? OnRequestRepaint;

        public LevelAIChatViewModel(
            ILevelAIChatService aiService,
            LevelEditorViewModel editorViewModel,
            GridManipulator gridManipulator)
        {
            _aiService = aiService;
            _editorViewModel = editorViewModel;
            _intentExecutor = new IntentExecutor(editorViewModel, gridManipulator);

            // 添加欢迎消息
            Messages.Add(ChatMessage.Assistant(
                "你好！我是关卡编辑助手。你可以用自然语言描述你想要的关卡，比如：\n" +
                "- \"创建一个 8x8 的网格\"\n" +
                "- \"步数限制设为 20\"\n" +
                "- \"目标是消除 30 个红色方块\"\n" +
                "- \"在中间放一个彩虹炸弹\"\n" +
                "- \"生成一个简单难度的关卡\""));
        }

        public async Task SendMessageAsync()
        {
            if (string.IsNullOrWhiteSpace(InputText) || IsWaitingResponse)
                return;

            var userMessage = InputText.Trim();
            InputText = "";

            // 添加用户消息
            Messages.Add(ChatMessage.User(userMessage));
            OnMessagesChanged?.Invoke();

            IsWaitingResponse = true;
            _cts = new CancellationTokenSource();

            try
            {
                // 构建上下文
                var context = LevelContextBuilder.Build(
                    _editorViewModel.ActiveLevelConfig,
                    _editorViewModel.WinRate,
                    _editorViewModel.DifficultyText);

                // 调用 AI 服务
                var response = await _aiService.SendMessageAsync(
                    userMessage,
                    context,
                    Messages,
                    _cts.Token);

                if (response.Success)
                {
                    // 添加 AI 回复
                    Messages.Add(ChatMessage.Assistant(response.Message ?? "", response.Intents));

                    // 执行意图
                    if (response.Intents != null && response.Intents.Count > 0)
                    {
                        foreach (var intent in response.Intents)
                        {
                            _intentExecutor.Execute(intent);
                        }
                        OnRequestRepaint?.Invoke();
                    }
                }
                else
                {
                    Messages.Add(ChatMessage.Error(response.Error ?? "请求失败"));
                }
            }
            catch (OperationCanceledException)
            {
                Messages.Add(ChatMessage.Error("请求已取消"));
            }
            catch (Exception ex)
            {
                Messages.Add(ChatMessage.Error($"错误: {ex.Message}"));
            }
            finally
            {
                IsWaitingResponse = false;
                OnMessagesChanged?.Invoke();
            }
        }

        public void CancelRequest()
        {
            _cts?.Cancel();
        }

        public void ClearHistory()
        {
            Messages.Clear();
            Messages.Add(ChatMessage.Assistant("对话已清空，有什么可以帮你的？"));
            OnMessagesChanged?.Invoke();
        }

        public void ToggleVisibility()
        {
            IsVisible = !IsVisible;
        }

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public void Dispose()
        {
            _cts?.Cancel();
            _cts?.Dispose();
        }
    }
}
