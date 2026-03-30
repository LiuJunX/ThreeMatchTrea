"""Render a level JSON to a PNG image for visual evaluation."""
import json, sys, os
import matplotlib.pyplot as plt
import matplotlib.patches as mpatches
from matplotlib.patches import FancyBboxPatch
import numpy as np

# Color palette
C_VOID = '#2a2a2a'
C_SLOT = '#e8dcc8'
C_SPAWNER = '#b8c9e0'
C_SINK = '#c9e0b8'
C_KEEPEMPTY = '#f5f0e0'

C_BOX = '#c8956c'
C_BUSH = '#6ab04c'
C_SAFE = '#7f8c8d'
C_COLORBOX = '#e74c3c'
C_CURTAIN = '#9b59b6'
C_MAGICHAT = '#f39c12'
C_MAILBOX = '#3498db'
C_OWL = '#95a5a6'
C_STONE = '#636e72'
C_POTIONBOTTLE = '#e056a0'

C_ICE = '#a8d8ea'
C_GRASS = '#a8e6a1'
C_LEAVES = '#7ec880'

C_CAGE = '#e74c3c'
C_CHAIN = '#d4a017'
C_HONEY = '#f5b041'
C_FROST = '#85c1e9'
C_BUBBLE = '#d5a6ff'

OBSTACLE_COLORS = {
    'Box': C_BOX, 'Bush': C_BUSH, 'Safe': C_SAFE, 'ColorBox': C_COLORBOX,
    'Curtain': C_CURTAIN, 'MagicHat': C_MAGICHAT, 'Mailbox': C_MAILBOX,
    'Owl': C_OWL, 'Stone': C_STONE, 'PotionBottle': C_POTIONBOTTLE,
    'Cupboard': C_BOX,
}
OBSTACLE_LABELS = {
    'Box': 'Bx', 'Bush': 'Bu', 'Safe': 'Sf', 'ColorBox': 'Cx',
    'Curtain': 'Cu', 'MagicHat': 'Mh', 'Mailbox': 'Mb',
    'Owl': 'Ow', 'Stone': 'St', 'PotionBottle': 'Pb', 'Cupboard': 'Cp',
}
GROUND_COLORS = {'Ice': C_ICE, 'Grass': C_GRASS, 'Leaves': C_LEAVES}
COVER_COLORS = {
    'Cage': C_CAGE, 'Chain': C_CHAIN, 'Honey': C_HONEY,
    'Frost': C_FROST, 'Bubble': C_BUBBLE,
}


def load_level(path):
    with open(path, 'r', encoding='utf-8') as f:
        return json.load(f)


def get_array(data, key, w, h, default='None'):
    arr = data.get(key)
    if arr is None:
        return [default] * (w * h)
    if isinstance(arr, str):
        # base64 encoded, skip for now
        return [default] * (w * h)
    return arr


def render(data, out_path):
    w = data['width']
    h = data['height']
    name = data.get('name', data.get('id', ''))
    moves = data.get('moveLimit', '?')

    cells = get_array(data, 'cells', w, h, 'Slot')
    grid = get_array(data, 'grid', w, h, 'None')
    obstacles = get_array(data, 'obstacles', w, h, 'None')
    grounds = get_array(data, 'grounds', w, h, 'None')
    covers = get_array(data, 'covers', w, h, 'None')
    obs_stages = get_array(data, 'obstacleStages', w, h, '0')

    cell_size = 1.0
    margin = 0.08
    fig_w = w * cell_size + 1.5
    fig_h = h * cell_size + 2.0
    fig, ax = plt.subplots(1, 1, figsize=(fig_w, fig_h), dpi=120)
    ax.set_xlim(-0.5, w - 0.5 + 0.01)
    ax.set_ylim(h - 0.5 + 0.01, -0.5)
    ax.set_aspect('equal')
    ax.axis('off')

    # Title
    objectives = data.get('objectives', [])
    obj_str = ' + '.join(
        f"{o.get('targetLayer','?')}/{o.get('targetCount','?')}"
        for o in objectives
    )
    ax.set_title(f"{name}  ({w}×{h}, {moves} moves)\n{obj_str}",
                 fontsize=11, fontweight='bold', pad=12)

    for y in range(h):
        for x in range(w):
            idx = y * w + x
            cell = cells[idx] if idx < len(cells) else 'Slot'
            grd = grounds[idx] if idx < len(grounds) else 'None'
            obs = obstacles[idx] if idx < len(obstacles) else 'None'
            cov = covers[idx] if idx < len(covers) else 'None'
            gri = grid[idx] if idx < len(grid) else 'None'
            stage = obs_stages[idx] if idx < len(obs_stages) else 0
            if isinstance(stage, str):
                try:
                    stage = int(stage)
                except:
                    stage = 0

            # Base cell color
            if cell == 'Void':
                bg = C_VOID
            elif cell == 'Spawner':
                bg = C_SPAWNER
            elif cell == 'Sink':
                bg = C_SINK
            else:
                bg = C_SLOT

            # KeepEmpty override
            if gri == 'KeepEmpty' and cell != 'Void':
                bg = C_KEEPEMPTY

            # Draw cell background
            rect = FancyBboxPatch(
                (x - 0.5 + margin, y - 0.5 + margin),
                cell_size - 2 * margin, cell_size - 2 * margin,
                boxstyle=f"round,pad={margin*0.5}",
                facecolor=bg, edgecolor='#888' if cell != 'Void' else C_VOID,
                linewidth=0.5 if cell != 'Void' else 0
            )
            ax.add_patch(rect)

            if cell == 'Void':
                continue

            # Ground layer (background tint)
            if grd != 'None' and grd in GROUND_COLORS:
                ground_rect = FancyBboxPatch(
                    (x - 0.35, y - 0.35), 0.7, 0.7,
                    boxstyle="round,pad=0.05",
                    facecolor=GROUND_COLORS[grd], edgecolor='none',
                    alpha=0.5, linewidth=0
                )
                ax.add_patch(ground_rect)

            # Cover layer (border ring)
            if cov != 'None' and cov in COVER_COLORS:
                cover_rect = FancyBboxPatch(
                    (x - 0.38, y - 0.38), 0.76, 0.76,
                    boxstyle="round,pad=0.02",
                    facecolor='none',
                    edgecolor=COVER_COLORS[cov],
                    linewidth=2.5, alpha=0.9
                )
                ax.add_patch(cover_rect)

            # Obstacle layer (center marker)
            if obs != 'None' and obs in OBSTACLE_COLORS:
                color = OBSTACLE_COLORS[obs]
                circle = plt.Circle((x, y), 0.28, facecolor=color,
                                    edgecolor='white', linewidth=1.2, alpha=0.9)
                ax.add_patch(circle)
                label = OBSTACLE_LABELS.get(obs, obs[:2])
                if stage > 1:
                    label = f"{label}{stage}"
                ax.text(x, y, label, ha='center', va='center',
                        fontsize=6.5, fontweight='bold', color='white')

            # Spawner/Sink label
            if cell == 'Spawner':
                ax.text(x, y, '▼', ha='center', va='center',
                        fontsize=8, color='#5a7ea6', alpha=0.6)
            elif cell == 'Sink':
                ax.text(x, y, '▽', ha='center', va='center',
                        fontsize=8, color='#6a9a5a', alpha=0.6)

            # Ground label
            if grd != 'None' and obs == 'None':
                gl = {'Ice': '❄', 'Grass': '♣', 'Leaves': '♧'}.get(grd, '')
                ax.text(x, y, gl, ha='center', va='center',
                        fontsize=10, color='#555', alpha=0.7)

    # Legend
    legend_items = []
    # Collect what's actually used
    used_obs = set(o for o in obstacles if o != 'None')
    used_grd = set(g for g in grounds if g != 'None')
    used_cov = set(c for c in covers if c != 'None')

    for name_key in sorted(used_obs):
        if name_key in OBSTACLE_COLORS:
            legend_items.append(mpatches.Patch(
                color=OBSTACLE_COLORS[name_key], label=name_key))
    for name_key in sorted(used_grd):
        if name_key in GROUND_COLORS:
            legend_items.append(mpatches.Patch(
                color=GROUND_COLORS[name_key], label=name_key))
    for name_key in sorted(used_cov):
        if name_key in COVER_COLORS:
            legend_items.append(mpatches.Patch(
                color=COVER_COLORS[name_key], label=name_key))

    if legend_items:
        ax.legend(handles=legend_items, loc='upper center',
                  bbox_to_anchor=(0.5, -0.02), ncol=min(len(legend_items), 5),
                  fontsize=7, frameon=False)

    plt.tight_layout()
    plt.savefig(out_path, bbox_inches='tight', facecolor='white')
    plt.close()
    print(f"Saved: {out_path}")


if __name__ == '__main__':
    if len(sys.argv) < 2:
        print("Usage: python render_level.py <level.json> [output.png]")
        sys.exit(1)

    level_path = sys.argv[1]
    out = sys.argv[2] if len(sys.argv) > 2 else level_path.replace('.json', '.png')
    data = load_level(level_path)
    render(data, out)
