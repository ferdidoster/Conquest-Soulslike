"""
Shared detailing for the lab kit, after the concept sheet's asset callouts: heavy chamfered steel,
framed plates with recessed panel lines, bolt rows, corner guards and small orange status lights.
All helpers work in a piece's local space (X along the front, -Y toward the viewer, Z up).
"""

from . import palette as P


def bolts_row(b, x0, x1, y, z, n, size=0.05, mat=P.METAL):
    """n bolt heads on a front face (facing -Y at depth y) from x0 to x1 at height z."""
    for i in range(n):
        x = x0 + (x1 - x0) * (i / (n - 1) if n > 1 else 0.5)
        b.box((size, 0.03, size), (x, y - 0.015, z - size / 2), mat)


def framed_plate(b, w, h, cx, y, z0, depth=0.06, frame=0.07, mat=P.WALL, frame_mat=P.METAL_DARK,
                 seams=0, bolts=0, face=-1):
    """A plate of w x h on a face at depth y (face=-1 looks toward -Y): raised frame, recessed
    centre, optional vertical seams and corner bolts."""
    out = face
    b.box((w, depth, h), (cx, y + out * depth / 2, z0), frame_mat, bevel=0.012)
    b.box((w - 2 * frame, depth * 0.5, h - 2 * frame), (cx, y + out * depth * 0.95, z0 + frame), mat,
          bevel=0.008)
    for i in range(seams):
        x = cx - w / 2 + w * (i + 1) / (seams + 1)
        b.box((0.025, depth * 0.3, h - 2 * frame), (x, y + out * depth * 1.25, z0 + frame), frame_mat)
    if bolts:
        for sx in (-1, 1):
            for z in (z0 + frame * 0.5, z0 + h - frame * 0.5):
                b.box((0.04, 0.02, 0.04), (cx + sx * (w / 2 - frame * 0.5), y + out * (depth + 0.01), z - 0.02),
                      P.METAL)


def chamfer_frame(b, w, h, t, d, cx, cy, z0, chamfer, mat=P.METAL_DARK):
    """A door-style frame: two posts, a lintel and chamfered upper corners (the concept's doors)."""
    b.box((t, d, h - chamfer), (cx - w / 2 + t / 2, cy, z0), mat, bevel=0.02)
    b.box((t, d, h - chamfer), (cx + w / 2 - t / 2, cy, z0), mat, bevel=0.02)
    b.box((w - 2 * chamfer, d, t), (cx, cy, z0 + h - t), mat, bevel=0.02)
    for s in (-1, 1):
        # 45 degree corner brace
        L = chamfer * 1.42
        b.box((L, d, t), (cx + s * (w / 2 - chamfer / 2 - t * 0.15), cy, z0 + h - chamfer / 2 - t / 2), mat,
              rot_y=s * 45, base=False, bevel=0.02)


def status_light(b, x, y, z, w=0.08, h=0.05, mat=P.LAMP_WARM):
    """Small glowing indicator with a dark housing, on a face at depth y."""
    b.box((w + 0.04, 0.03, h + 0.04), (x, y - 0.01, z - 0.02), P.METAL_DARK)
    b.box((w, 0.03, h), (x, y - 0.03, z), mat)


def corner_guards(b, w, d, h, z0=0.0, size=0.12, mat=P.METAL_DARK):
    """Vertical L guards on the four corners of a w x d box."""
    for sx in (-1, 1):
        for sy in (-1, 1):
            b.box((size, size, h + 0.02), (sx * (w / 2 - size / 2 + 0.015), sy * (d / 2 - size / 2 + 0.015), z0 - 0.01),
                  mat, bevel=0.01)
