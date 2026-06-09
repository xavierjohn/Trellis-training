"""Generate the Order Lifecycle state-machine diagram (order-lifecycle.png).

Mirrors the OM reference state machine (after/OrderManagement/Domain/src/
Aggregates/Order.cs) and spec section 4:

    Draft --Submit--> Submitted --Approve--> Approved --Ship--> Shipped --Deliver--> Delivered
    Draft / Submitted / Approved --Cancel--> Cancelled

Stock is RESERVED on Submit and RELEASED on Cancel ONLY from Submitted or
Approved (spec 4: "If order was Submitted or Approved, release reserved stock").
Cancelling a Draft reserves nothing, so it releases nothing.

Run with: python docs/images/gen_order_lifecycle.py
"""

from pathlib import Path

import matplotlib.pyplot as plt
from matplotlib.patches import FancyBboxPatch

# name, x-center, fill, edge
states = {
    "Draft": (1.35, "#8a9199", "#5f666d"),
    "Submitted": (3.675, "#3f8ee0", "#246cc4"),
    "Approved": (6.0, "#34a673", "#1f7d54"),
    "Shipped": (8.325, "#e8902f", "#c0701c"),
    "Delivered": (10.65, "#9b59c4", "#743f96"),
}
cancelled = ("#e0483f", "#b5322b")

top_y, bot_y, bw, bh = 4.4, 1.4, 1.7, 0.95

fig, ax = plt.subplots(figsize=(12, 5.6))
ax.set_xlim(0, 12)
ax.set_ylim(0, 6.2)
ax.axis("off")
fig.patch.set_facecolor("#f7f8fa")

ax.text(6, 5.75, "Order Lifecycle", ha="center", va="center",
        fontsize=22, fontweight="bold", color="#1a2744")


def box(cx, cy, label, fill, edge):
    ax.add_patch(FancyBboxPatch(
        (cx - bw / 2, cy - bh / 2), bw, bh,
        boxstyle="round,pad=0.02,rounding_size=0.12",
        linewidth=2.0, edgecolor=edge, facecolor=fill, alpha=0.97))
    ax.text(cx, cy, label, ha="center", va="center",
            fontsize=14, fontweight="bold", color="#ffffff")


for name, (cx, fill, edge) in states.items():
    box(cx, top_y, name, fill, edge)

# Forward transitions along the happy path.
transitions = [
    ("Draft", "Submitted", "Submit", "Reserve Stock"),
    ("Submitted", "Approved", "Approve", None),
    ("Approved", "Shipped", "Ship", None),
    ("Shipped", "Delivered", "Deliver", None),
]
for frm, to, label, sub in transitions:
    x0 = states[frm][0] + bw / 2
    x1 = states[to][0] - bw / 2
    ax.annotate("", xy=(x1, top_y), xytext=(x0, top_y),
                arrowprops=dict(arrowstyle="-|>", lw=2.4, color="#3a3a3a",
                                mutation_scale=20))
    mid = (x0 + x1) / 2
    ax.text(mid, top_y + 0.62, label, ha="center", va="bottom",
            fontsize=12, fontweight="bold", color="#222222")
    if sub:
        ax.text(mid, top_y - 0.62, sub, ha="center", va="top",
                fontsize=10, style="italic", color="#246cc4")

# Cancel transitions. Release Stock ONLY from Submitted / Approved.
cancel_from = [("Draft", False), ("Submitted", True), ("Approved", True)]
for name, releases in cancel_from:
    cx = states[name][0]
    box(cx, bot_y, "Cancelled", *cancelled)
    ax.annotate("", xy=(cx, bot_y + bh / 2), xytext=(cx, top_y - bh / 2),
                arrowprops=dict(arrowstyle="-|>", lw=2.0, color="#b5322b",
                                mutation_scale=18))
    ax.text(cx + 0.22, 3.15, "Cancel", ha="left", va="center",
            fontsize=11, fontweight="bold", color="#b5322b")
    if releases:
        ax.text(cx + 0.22, 2.75, "Release Stock", ha="left", va="center",
                fontsize=9.5, style="italic", color="#b5322b")

out = Path(__file__).with_name("order-lifecycle.png")
fig.savefig(out, dpi=150, bbox_inches="tight", facecolor=fig.get_facecolor())
print("wrote", out)
