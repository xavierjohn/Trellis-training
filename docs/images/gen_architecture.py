"""Generate the Clean Architecture Overview diagram (architecture-overview.png).

Reflects the actual dependency direction of the OM reference implementation
(after/OrderManagement): outer layers depend inward.

    API  ->  Anti-Corruption Layer  ->  Application  ->  Domain
    API  ----------------------------->  Application   (skip dependency)

The Anti-Corruption Layer (EF Core) is an OUTER layer: it implements the
repository interfaces declared in Application (Dependency Inversion), so it
depends on Application rather than sitting between Application and Domain.

Source of truth: the *.csproj ProjectReference graph under after/OrderManagement.
Run with: python docs/images/gen_architecture.py
"""

from pathlib import Path

import matplotlib.pyplot as plt
from matplotlib.patches import FancyBboxPatch

# Layers top -> bottom (outermost to innermost): name, contents, fill, edge.
layers = [
    ("API", "Controllers  ·  DTOs  ·  Middleware", "#6aa9e0", "#2f6db5"),
    ("Anti-Corruption Layer",
     "EF Core (DbContext)  ·  Repositories  ·  Configurations", "#f3b05a", "#d98a2b"),
    ("Application",
     "Commands · Queries · Handlers · Authorization · Repository Interfaces",
     "#8fce86", "#4e9e44"),
    ("Domain",
     "Aggregates  ·  Entities  ·  Value Objects  ·  Events  ·  Specifications",
     "#c3a6e0", "#8a5fc0"),
]

fig, ax = plt.subplots(figsize=(9.8, 5.9))
ax.set_xlim(0, 10)
ax.set_ylim(0, 10)
ax.axis("off")
fig.patch.set_facecolor("#ffffff")

ax.text(5, 9.45, "Clean Architecture Overview", ha="center", va="center",
        fontsize=23, fontweight="bold", color="#1a2744")

box_left, box_w, box_h = 0.8, 7.5, 1.25
centers_y = [8.0, 6.2, 4.4, 2.6]
cx = box_left + box_w / 2

for (name, sub, fill, edge), cy in zip(layers, centers_y):
    ax.add_patch(FancyBboxPatch(
        (box_left, cy - box_h / 2), box_w, box_h,
        boxstyle="round,pad=0.02,rounding_size=0.14",
        linewidth=2.2, edgecolor=edge, facecolor=fill, alpha=0.95))
    ax.text(cx, cy + 0.27, name, ha="center", va="center",
            fontsize=16, fontweight="bold", color="#16242f")
    ax.text(cx, cy - 0.28, sub, ha="center", va="center",
            fontsize=10.5 if name == "Application" else 11, color="#1f2d24")

# Main dependency chain: each box depends on the one below it (arrow points down).
for i in range(3):
    ax.annotate("", xy=(cx, centers_y[i + 1] + box_h / 2),
                xytext=(cx, centers_y[i] - box_h / 2),
                arrowprops=dict(arrowstyle="-|>", lw=2.6, color="#3a3a3a",
                                mutation_scale=22))

# DIP annotation on the ACL -> Application arrow.
ax.text(cx + 0.35, (centers_y[1] + centers_y[2]) / 2,
        "implements repository\ninterfaces (DIP)", ha="left", va="center",
        fontsize=9, style="italic", color="#555555")

# Skip dependency: API also depends directly on Application (bypassing ACL).
box_right = box_left + box_w
ax.annotate("", xy=(box_right, centers_y[2] + 0.15),
            xytext=(box_right, centers_y[0] - 0.15),
            arrowprops=dict(arrowstyle="-|>", lw=2.0, color="#2f6db5",
                            connectionstyle="arc3,rad=-0.32", mutation_scale=18))
ax.text(box_right + 1.18, (centers_y[0] + centers_y[2]) / 2,
        "API → Application", rotation=90, ha="center", va="center",
        fontsize=9, color="#2f6db5")

ax.text(5, 1.15,
        "Arrows show dependency direction — outer layers depend inward. The "
        "Anti-Corruption Layer\nimplements the repository interfaces defined in "
        "Application, so it depends on Application.",
        ha="center", va="center", fontsize=9.5, color="#666666")

out = Path(__file__).with_name("architecture-overview.png")
fig.savefig(out, dpi=150, bbox_inches="tight", facecolor=fig.get_facecolor())
print("wrote", out)
