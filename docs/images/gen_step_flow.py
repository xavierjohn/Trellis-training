"""Generate the lab step-flow diagram (step-flow.png) for the README.

Mirrors the 8-step procedure in the README "How a lab works" table. Steps 4
and 8 are the AI-driven steps (highlighted green); step 8 is intentionally
lab-agnostic ("Add Feature") because the feature differs per lab (OM: Order
Returns, worker: SLA policy override, URL shortener: bulk-import endpoint).

Run with: python docs/images/gen_step_flow.py
"""

from pathlib import Path

import matplotlib.pyplot as plt
from matplotlib.patches import FancyBboxPatch

# (number, label) — order and wording follow the README 8-step table.
steps = [
    (1, "Create\nProject"),
    (2, "Aspire\nDashboard"),
    (3, "Scaffold"),
    (4, "AI\nImplements"),
    (5, "Smoke\nTest"),
    (6, "Review"),
    (7, "Feedback"),
    (8, "Add\nFeature"),
]
ai_steps = {4, 8}  # AI-driven, highlighted

n = len(steps)
left, right, bw, cy, bh = 0.35, 14.65, 1.45, 1.6, 1.5
gap = ((right - left) - n * bw) / (n - 1)
centers = [left + bw / 2 + i * (bw + gap) for i in range(n)]

blue_fill, blue_edge = "#ffffff", "#2f6db5"
green_fill, green_edge = "#d8edc8", "#4e9e44"

fig, ax = plt.subplots(figsize=(15, 2.9))
ax.set_xlim(0, 15)
ax.set_ylim(0, 3.4)
ax.axis("off")
fig.patch.set_facecolor("#ffffff")

ax.text(7.5, 3.12, "How a Lab Works", ha="center", va="center",
        fontsize=20, fontweight="bold", color="#1a2744")

for (num, label), cx in zip(steps, centers):
    is_ai = num in ai_steps
    fill = green_fill if is_ai else blue_fill
    edge = green_edge if is_ai else blue_edge
    ax.add_patch(FancyBboxPatch(
        (cx - bw / 2, cy - bh / 2), bw, bh,
        boxstyle="round,pad=0.02,rounding_size=0.16",
        linewidth=2.0, edgecolor=edge, facecolor=fill))
    ax.plot([cx], [cy + 0.42], marker="o", markersize=24,
            markerfacecolor="#ffffff", markeredgecolor=edge,
            markeredgewidth=2.0, zorder=4)
    ax.text(cx, cy + 0.42, str(num), ha="center", va="center",
            fontsize=12, fontweight="bold", color=edge, zorder=5)
    ax.text(cx, cy - 0.34, label, ha="center", va="center",
            fontsize=11.5, color="#16242f")

# Arrows between steps; green where they touch an AI-driven step.
for i in range(n - 1):
    touches_ai = steps[i][0] in ai_steps or steps[i + 1][0] in ai_steps
    color = green_edge if touches_ai else blue_edge
    ax.annotate("", xy=(centers[i + 1] - bw / 2, cy),
                xytext=(centers[i] + bw / 2, cy),
                arrowprops=dict(arrowstyle="-|>", lw=2.2, color=color,
                                mutation_scale=16))

ax.text(7.5, 0.42, "Steps 4 and 8 are AI-driven.", ha="center", va="center",
        fontsize=10, style="italic", color="#4e9e44")

out = Path(__file__).with_name("step-flow.png")
fig.savefig(out, dpi=150, bbox_inches="tight", facecolor=fig.get_facecolor())
print("wrote", out)
