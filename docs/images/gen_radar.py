"""Generate the Evaluation radar chart (evaluation-radar.png) for the README.

Overlays every model in the current alpha.360 cohort across the 5 scoring
levels. Each axis is normalized to its maximum point value, so the outer ring
represents full marks on that level.

Source data: the "Current cohort" table in results/evaluation-results.md.
Run with: python docs/images/gen_radar.py
"""

from pathlib import Path

import numpy as np
import matplotlib.pyplot as plt

# Axis definitions in plot order: (label, max points, ha, va).
axes = [
    ("L1: Structural", 18, "center", "bottom"),
    ("L2: Behavioral", 13, "left", "center"),
    ("L3: Architecture", 13, "left", "center"),
    ("L4: Tests", 9, "right", "center"),
    ("L5: Feedback", 4, "right", "center"),
]

# Per-model raw scores in axis order [L1, L2, L3, L4, L5], plus total and color.
models = [
    ("Claude Opus 4.8", 56, [18, 13, 13, 8, 4], "#2563eb"),    # signature blue, top
    ("Claude Sonnet 4.6", 56, [17, 13, 13, 9, 4], "#0d9488"),  # teal
    ("Claude Opus 4.7 1M", 55, [18, 13, 13, 7, 4], "#7c3aed"), # purple
    ("GPT-5.5", 54, [18, 13, 12, 7, 4], "#ea580c"),            # orange
    ("Claude Haiku 4.5", 43, [15, 11, 8, 7, 2], "#dc2626"),    # red, only FAIL
]

maxima = np.array([a[1] for a in axes], dtype=float)
n = len(axes)

# Evenly spaced axes; the polar transform handles "start at top, go clockwise".
angles = np.linspace(0, 2 * np.pi, n, endpoint=False)
closed_angles = np.concatenate([angles, angles[:1]])

fig, ax = plt.subplots(figsize=(8.6, 8), subplot_kw=dict(polar=True))
fig.patch.set_facecolor("#fbfbfb")
ax.set_facecolor("#fbfbfb")

ax.set_theta_zero_location("N")
ax.set_theta_direction(-1)
ax.set_ylim(0, 1.0)

for name, total, scores, color in models:
    vals = np.array(scores, dtype=float) / maxima
    closed_vals = np.concatenate([vals, vals[:1]])
    ax.plot(closed_angles, closed_vals, color=color, linewidth=2.4,
            marker="o", markersize=5, zorder=5, label=f"{name}  ({total}/57)")
    ax.fill(closed_angles, closed_vals, color=color, alpha=0.07, zorder=2)

# Two-line axis labels (level + point budget) placed just outside the ring.
ax.set_xticks(angles)
ax.set_xticklabels([])
for ang, (label, pts, ha, va) in zip(angles, axes):
    ax.text(ang, 1.20, f"{label}\n({pts} pts)", ha=ha, va=va,
            fontsize=14, color="#1a2744")

# Concentric grid rings, no numeric radial labels (matches original style).
ax.set_yticks([0.25, 0.5, 0.75, 1.0])
ax.set_yticklabels([])
ax.yaxis.grid(True, color="#c8c8c8", linewidth=0.8)
ax.xaxis.grid(True, color="#bdbdbd", linewidth=0.8)
ax.spines["polar"].set_color("#333333")
ax.spines["polar"].set_linewidth(1.2)

legend = ax.legend(loc="center left", bbox_to_anchor=(1.04, 0.5),
                   fontsize=12, frameon=True, borderpad=0.8, labelspacing=0.6)
legend.get_frame().set_edgecolor("#bdbdbd")
legend.get_frame().set_facecolor("#ffffff")

out = Path(__file__).with_name("evaluation-radar.png")
fig.savefig(out, dpi=150, bbox_inches="tight", facecolor=fig.get_facecolor())
print("wrote", out)
