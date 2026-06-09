"""Generate the Railway-Oriented Programming diagram (rop-pipeline.png).

Models the real OM SubmitOrder handler + Trellis pipeline:

    FindById --> Submit --> Commit (Unit of Work) --> 200 OK (Order -> DTO)
       |           |             |
    NotFound   Insufficient   DbError  ------------> ProblemDetails (RFC 9457)
                  Stock

The commit is a framework pipeline stage (AddTrellisUnitOfWork<AppDbContext>);
handlers never call SaveChanges. Any step returning a failed Result short-
circuits onto the error track.

Run with: python docs/images/gen_rop_pipeline.py
"""

from pathlib import Path

import matplotlib.pyplot as plt
from matplotlib.patches import FancyBboxPatch

bw, bh = 2.0, 0.9
y_s, y_f = 4.3, 1.4  # success rail / failure rail
centers = [1.5, 4.833, 8.166, 11.5]

# (title, subtitle, fill, edge)
success = [
    ("FindById", "load order", "#eaf3fb", "#2f6db5"),
    ("Submit", "reserve stock", "#eaf3fb", "#2f6db5"),
    ("Commit", "Unit of Work", "#fff4e6", "#d98a2b"),   # framework stage
    ("200 OK", "Order \u2192 DTO", "#d8edc8", "#4e9e44"),
]
failure = [
    ("NotFound", None, "#fbe4e2", "#b5322b"),
    ("Insufficient Stock", None, "#fbe4e2", "#b5322b"),
    ("DbError", None, "#fbe4e2", "#b5322b"),
    ("ProblemDetails", "RFC 9457", "#f3c0ba", "#b5322b"),
]

fig, ax = plt.subplots(figsize=(12.5, 5.2))
ax.set_xlim(0, 13)
ax.set_ylim(0, 6)
ax.axis("off")
fig.patch.set_facecolor("#fbfbfb")

ax.text(6.5, 5.6, "Railway-Oriented Programming", ha="center", va="center",
        fontsize=21, fontweight="bold", color="#1a2744")
ax.text(6.5, 5.12, "SubmitOrder handler — a failed Result at any step "
        "short-circuits onto the error track", ha="center", va="center",
        fontsize=11, color="#555555")

# Rails behind the boxes.
ax.plot([0.4, 12.6], [y_s, y_s], color="#4e9e44", lw=2.0, zorder=1)
ax.plot([0.4, 12.6], [y_f, y_f], color="#b5322b", lw=2.0, zorder=1)
ax.text(0.4, y_s + 0.55, "success", color="#4e9e44", fontsize=9.5,
        style="italic", ha="left")
ax.text(0.4, y_f - 0.6, "failure", color="#b5322b", fontsize=9.5,
        style="italic", ha="left")


def draw_box(cx, cy, title, sub, fill, edge, bold_size=13):
    ax.add_patch(FancyBboxPatch(
        (cx - bw / 2, cy - bh / 2), bw, bh,
        boxstyle="round,pad=0.02,rounding_size=0.12",
        linewidth=2.0, edgecolor=edge, facecolor=fill, zorder=3))
    dy = 0.16 if sub else 0.0
    ax.text(cx, cy + dy, title, ha="center", va="center",
            fontsize=bold_size, fontweight="bold", color="#16242f", zorder=4)
    if sub:
        ax.text(cx, cy - 0.22, sub, ha="center", va="center",
                fontsize=9.5, color="#3a3a3a", zorder=4)


for cx, (t, s, f, e) in zip(centers, success):
    draw_box(cx, y_s, t, s, f, e)
for cx, (t, s, f, e) in zip(centers, failure):
    draw_box(cx, y_f, t, s, f, e)

# Forward arrows along each rail.
for i in range(3):
    ax.annotate("", xy=(centers[i + 1] - bw / 2, y_s),
                xytext=(centers[i] + bw / 2, y_s),
                arrowprops=dict(arrowstyle="-|>", lw=2.2, color="#4e9e44",
                                mutation_scale=18, zorder=2))
    ax.annotate("", xy=(centers[i + 1] - bw / 2, y_f),
                xytext=(centers[i] + bw / 2, y_f),
                arrowprops=dict(arrowstyle="-|>", lw=2.0, color="#b5322b",
                                mutation_scale=16, zorder=2))

# Drop arrows from each fallible step to its error box.
for cx in centers[:3]:
    ax.annotate("", xy=(cx, y_f + bh / 2), xytext=(cx, y_s - bh / 2),
                arrowprops=dict(arrowstyle="-|>", lw=1.8, color="#b5322b",
                                mutation_scale=15, zorder=2,
                                connectionstyle="arc3,rad=0.0"))

# Mark the commit as a framework pipeline stage.
ax.annotate("framework pipeline —\nno SaveChanges in the handler",
            xy=(centers[2] + bw / 2 - 0.3, y_s - bh / 2),
            xytext=(9.8, 3.15),
            ha="center", va="center", fontsize=9, style="italic",
            color="#d98a2b",
            arrowprops=dict(arrowstyle="-", lw=1.0, color="#d98a2b"))

ax.text(6.5, 0.35, "No try/catch — Bind / Map / Ensure thread the Result through; "
        "the first failure skips the remaining steps.",
        ha="center", va="center", fontsize=10, color="#666666")

out = Path(__file__).with_name("rop-pipeline.png")
fig.savefig(out, dpi=150, bbox_inches="tight", facecolor=fig.get_facecolor())
print("wrote", out)
