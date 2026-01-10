using System.Collections.Generic;
using Match3.Editor.ViewModels;

namespace Match3.Editor.Interfaces
{
    public interface IScenarioService
    {
        ScenarioFolderNode BuildTree();
        string ReadScenarioJson(string relativePath);
        void WriteScenarioJson(string relativePath, string json);
        string CreateNewScenario(string folderRelativePath, string scenarioName, string json);
        string CreateFolder(string parentFolderRelativePath, string folderName);
        string DuplicateScenario(string sourceRelativePath, string newScenarioName);
        void DeleteScenario(string relativePath);
        void DeleteFolder(string relativePath);
        void RenameScenario(string relativePath, string newName);
        void RenameFolder(string relativePath, string newName);
    }
}
