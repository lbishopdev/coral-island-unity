#!/usr/bin/env python3
"""Headless verification harness for the Tide & Till terrain / grounding fix.

Unity is not available in this environment, so this script ports the mesh
construction used by ``Assets/TideAndTill/Scripts/World/WorldBuilder.cs``
(``SampleHeight`` / ``BuildIsland``) and the grounding contract used by
``Assets/TideAndTill/Scripts/Player/PlayerController.cs`` (``Move``), then
asserts:

  1. WINDING    the terrain triangle order *read from the real source file*
                produces face normals that all point up.
  2. NORMALS    freshly recalculated vertex normals also point up, so shading
                and ground-contact filtering agree with the surface.
  3. GROUNDING  a downward ground probe from the player's feet finds a
                front-facing terrain triangle at the spawn point and across the
                walkable island, so the CharacterController can become grounded
                instead of falling through.
  4. RECOVERY   the last-safe-position / out-of-bounds recovery loop settles
                instead of oscillating once grounding works.
  5. HEIGHT     the analytic ``SampleHeight()`` curve agrees with the built mesh
                surface within interpolation tolerance, so the spawn placement
                and prop placement sit on the visible ground.

The terrain winding is *parsed out of the C# source* rather than hard-coded, so
this is a genuine regression test: running it against the pre-fix tree fails.

Exit status is 0 when every assertion passes, 1 otherwise.

Assumptions (documented so the result can be judged honestly):

  * ``Mathf.PerlinNoise`` is reproduced structurally -- a standard Perlin with a
    fixed permutation table. Exact sample values will differ from Unity's
    internal table, but the winding, normals, grounding and recovery assertions
    do not depend on the noise values: only the height comparison does, and it
    is a tolerance check.
  * Unity's default ``Physics.queriesHitBackfaces`` is ``false``, and PhysX mesh
    raycasts ignore back faces. ``raycast_down`` models that, which is what makes
    triangle winding a *correctness* property for CharacterController grounding
    rather than a purely cosmetic one.
"""

from __future__ import annotations

import math
import os
import re
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.dirname(HERE)
WORLD_BUILDER = os.path.join(REPO, "Assets", "TideAndTill", "Scripts", "World", "WorldBuilder.cs")

# --------------------------------------------------------------------------- #
# Mathf ports
# --------------------------------------------------------------------------- #


def clamp01(v: float) -> float:
    return 0.0 if v < 0.0 else (1.0 if v > 1.0 else v)


def lerp(a: float, b: float, t: float) -> float:
    return a + (b - a) * clamp01(t)


def smooth_step(a: float, b: float, t: float) -> float:
    """Unity's Mathf.SmoothStep: Hermite-smoothed, clamped interpolation."""
    t = clamp01((t - a) / (b - a)) if b != a else 0.0
    return t * t * (3.0 - 2.0 * t)


# --- Perlin noise (structural port) ---------------------------------------- #

_PERM = []
_seed = 12345
for _ in range(256):
    _seed = (_seed * 1103515245 + 12345) & 0x7FFFFFFF
    _PERM.append(_seed % 256)


def _perm(i: int) -> int:
    return _PERM[i & 255]


def _fade(t: float) -> float:
    return t * t * t * (t * (t * 6.0 - 15.0) + 10.0)


def _grad(h: int, x: float, y: float) -> float:
    # The 8 gradients of the classic 2D Perlin set.
    return (
        (x, y, -x, -y, x + y, -x + y, x - y, -x - y)[h & 7]
    )


def perlin_noise(x: float, y: float) -> float:
    """2D Perlin noise scaled into [0, 1], matching Mathf.PerlinNoise's range."""
    xi, yi = int(math.floor(x)) & 255, int(math.floor(y)) & 255
    xf, yf = x - math.floor(x), y - math.floor(y)
    u, v = _fade(xf), _fade(yf)
    aa = _perm(_perm(xi) + yi)
    ab = _perm(_perm(xi) + yi + 1)
    ba = _perm(_perm(xi + 1) + yi)
    bb = _perm(_perm(xi + 1) + yi + 1)
    x1 = lerp(_grad(aa, xf, yf), _grad(ba, xf - 1.0, yf), u)
    x2 = lerp(_grad(ab, xf, yf - 1.0), _grad(bb, xf - 1.0, yf - 1.0), u)
    return clamp01(lerp(x1, x2, v) * 0.7071 + 0.5)


# --------------------------------------------------------------------------- #
# Source parsing: read the ACTUAL triangle winding out of WorldBuilder.cs
# --------------------------------------------------------------------------- #


def read_source_winding():
    """Return (vertex_names_of_triangle_a, vertex_names_of_triangle_b).

    Raises if the source no longer matches the expected ``triangles[t++] = ...``
    shape, so the harness fails loudly rather than silently passing.
    """
    with open(WORLD_BUILDER, "r", encoding="utf-8") as fh:
        src = fh.read()

    body = re.search(
        r"for \(int z = 0; z < zCount; z\+\+\).*?mesh\.triangles = triangles;",
        src,
        re.S,
    )
    if not body:
        raise RuntimeError("could not locate the terrain triangle loop in WorldBuilder.cs")

    assigns = re.findall(r"triangles\[t\+\+\]\s*=\s*([a-d]);", body.group(0))
    if len(assigns) != 6:
        raise RuntimeError(f"expected 6 triangle assignments, found {len(assigns)}: {assigns}")
    return tuple(assigns[:3]), tuple(assigns[3:])


A = 0  # z * (xCount + 1) + x
B = 1  # a + 1
C = 2  # a + xCount + 1
D = 3  # c + 1
NAME_TO_OFFSET = {"a": A, "b": B, "c": C, "d": D}


def offset_of(name):
    return NAME_TO_OFFSET[name]


# --------------------------------------------------------------------------- #
# WorldBuilder ports
# --------------------------------------------------------------------------- #

ISLAND_WIDTH = 88.0
ISLAND_DEPTH = 68.0
X_COUNT = 56
Z_COUNT = 44

# Player spawn contract from PlayerController.Create.
SPAWN_X, SPAWN_Z = -1.6, -4.5
SPAWN_Y_OFFSET = 0.12
STEP_OFFSET = 0.38
GRAVITY_Y = -9.81

# Grounding recovery constants, kept in step with PlayerController.
MAX_AIRBORNE_TIME = 1.5
SUNKEN_TOLERANCE = 0.35
SKIN = 0.02


def smooth_box(x, z, cx, cz, hx, hz, feather) -> float:
    dx = max(0.0, abs(x - cx) - hx)
    dz = max(0.0, abs(z - cz) - hz)
    return 1.0 - smooth_step(0.0, feather, math.sqrt(dx * dx + dz * dz))


def sample_height(x: float, z: float) -> float:
    """Port of WorldBuilder.SampleHeight."""
    nx = x / (ISLAND_WIDTH * 0.5)
    nz = z / (ISLAND_DEPTH * 0.5)
    ellipse = math.sqrt(nx * nx + nz * nz)
    shore_blend = smooth_step(0.0, 1.0, clamp01((1.03 - ellipse) / 0.13))
    shore = lerp(-1.6, 0.15, shore_blend)
    noise = (perlin_noise(x * 0.055 + 12.7, z * 0.055 + 4.1) - 0.5) * 0.85
    broad_hill = math.exp(-((x + 25.0) ** 2 + (z - 16.0) ** 2) / 360.0) * 2.2
    height = shore + noise * clamp01((1.02 - ellipse) * 4.0) + broad_hill
    height = lerp(height, 0.32, smooth_box(x, z, -8.0, -1.0, 16.0, 15.0, 5.0) * 0.94)
    height = lerp(height, 0.48, smooth_box(x, z, 23.0, 5.0, 14.0, 12.0, 4.0) * 0.88)
    return height


def build_island(vertex_offsets):
    """Port of WorldBuilder.BuildIsland, wound with the supplied offsets.

    ``vertex_offsets`` is a flat 6-tuple of a/b/c/d offsets, exactly as parsed
    from the C# source.
    """
    vertices, triangles = [], []
    for z in range(Z_COUNT + 1):
        for x in range(X_COUNT + 1):
            wx = lerp(-ISLAND_WIDTH * 0.5, ISLAND_WIDTH * 0.5, x / X_COUNT)
            wz = lerp(-ISLAND_DEPTH * 0.5, ISLAND_DEPTH * 0.5, z / Z_COUNT)
            vertices.append((wx, sample_height(wx, wz), wz))

    for z in range(Z_COUNT):
        for x in range(X_COUNT):
            corners = (z * (X_COUNT + 1) + x, x + 1 + z * (X_COUNT + 1),
                       (z + 1) * (X_COUNT + 1) + x, (z + 1) * (X_COUNT + 1) + x + 1)
            for k in range(0, 6, 3):
                triangles.append(corners[vertex_offsets[k]])
                triangles.append(corners[vertex_offsets[k + 1]])
                triangles.append(corners[vertex_offsets[k + 2]])
    return vertices, triangles


# --------------------------------------------------------------------------- #
# Mesh helpers
# --------------------------------------------------------------------------- #


def sub(a, b):
    return (a[0] - b[0], a[1] - b[1], a[2] - b[2])


def cross(u, v):
    return (u[1] * v[2] - u[2] * v[1], u[2] * v[0] - u[0] * v[2], u[0] * v[1] - u[1] * v[0])


def normalize(v):
    n = math.sqrt(v[0] * v[0] + v[1] * v[1] + v[2] * v[2])
    return (0.0, 0.0, 0.0) if n < 1e-12 else (v[0] / n, v[1] / n, v[2] / n)


def face_normals(vertices, triangles):
    """Unity's implicit triangle normal: Cross(v1 - v0, v2 - v0), normalized."""
    out = []
    for i in range(0, len(triangles), 3):
        a, b, c = (vertices[triangles[i + k]] for k in range(3))
        out.append(normalize(cross(sub(b, a), sub(c, a))))
    return out


def recalculate_normals(vertices, triangles):
    """Port of Mesh.RecalculateNormals: area-weighted accumulation, normalize."""
    normals = [[0.0, 0.0, 0.0] for _ in vertices]
    for i in range(0, len(triangles), 3):
        a, b, c = (vertices[triangles[i + k]] for k in range(3))
        n = cross(sub(b, a), sub(c, a))  # left unnormalized: area weighting
        for idx in (triangles[i], triangles[i + 1], triangles[i + 2]):
            normals[idx][0] += n[0]
            normals[idx][1] += n[1]
            normals[idx][2] += n[2]
    return [normalize(tuple(n)) for n in normals]


def raycast_down(vertices, triangles, ox, oy, oz):
    """Nearest downward hit against front-facing triangles only.

    Mirrors Physics.Raycast with Unity's default queriesHitBackfaces = false.
    Returns the hit y, or None.
    """
    best = None
    for i in range(0, len(triangles), 3):
        a, b, c = (vertices[triangles[i + k]] for k in range(3))
        n = normalize(cross(sub(b, a), sub(c, a)))
        if n[1] <= 1e-6:
            continue  # back face or edge-on: PhysX skips it
        d = (0.0, -1.0, 0.0)
        e1, e2 = sub(b, a), sub(c, a)
        h = cross(d, e2)
        det = e1[0] * h[0] + e1[1] * h[1] + e1[2] * h[2]
        if -1e-9 < det < 1e-9:
            continue
        inv = 1.0 / det
        s = (ox - a[0], oy - a[1], oz - a[2])
        u = (s[0] * h[0] + s[1] * h[1] + s[2] * h[2]) * inv
        if u < 0.0 or u > 1.0:
            continue
        q = cross(s, e1)
        v = (d[0] * q[0] + d[1] * q[1] + d[2] * q[2]) * inv
        if v < 0.0 or u + v > 1.0:
            continue
        t = (e2[0] * q[0] + e2[1] * q[1] + e2[2] * q[2]) * inv
        if t > 1e-6 and (best is None or t < best):
            best = t
    return None if best is None else oy - best


# --------------------------------------------------------------------------- #
# Simulation
# --------------------------------------------------------------------------- #


def ground_probe(vertices, triangles, x, z, feet_y):
    """Find the ground surface under the player.

    Casts from clearly above the character and returns the surface height, so the
    result does not depend on the character's current penetration depth. This is
    the same front-face-only query PhysX performs for a CharacterController:
    if the terrain triangles face down, no surface is reported at all.
    """
    origin = max(feet_y, sample_height(x, z)) + 2.0
    surface = raycast_down(vertices, triangles, x, origin, z)
    if surface is None:
        return None
    if surface > feet_y + STEP_OFFSET:
        return None  # surface is above the feet: the player is buried
    if feet_y - surface > STEP_OFFSET + SKIN:
        return None  # surface is too far below the feet: the player is airborne
    return surface


def simulate_player(vertices, triangles, frames=900, dt=1.0 / 60.0, events=None, start=None):
    """Port of the vertical half of PlayerController.Move, plus RecoverGrounding.

    ``events`` maps a frame index to a ``(kind, value)`` tuple:
      * ``("shove", dz)``  pushes the player past the island bounds
      * ``("drop", dy)``   lifts the player into the air
      * ``("bury", dy)``   pushes the player below the terrain surface
    ``start`` overrides the spawn position as ``(x, y, z)``.
    """
    events = events or {}
    if start is None:
        x, z = SPAWN_X, SPAWN_Z
        y = sample_height(x, z) + SPAWN_Y_OFFSET
    else:
        x, y, z = start

    vertical_velocity = 0.0
    last_safe = (x, y, z)
    airborne_time = 0.0
    recoveries = 0
    ever_grounded = False
    samples = []

    for frame in range(frames):
        kind, value = events.get(frame, (None, 0.0))
        if kind == "shove":
            z += value
        elif kind == "drop":
            y += value
        elif kind == "bury":
            y -= value

        hit = ground_probe(vertices, triangles, x, z, y)
        grounded = hit is not None

        if grounded:
            airborne_time = 0.0
            if vertical_velocity < 0.0:
                vertical_velocity = -2.4
        else:
            airborne_time += dt
            vertical_velocity += GRAVITY_Y * dt

        y += vertical_velocity * dt
        # CharacterController depenetration: skin width keeps the capsule just
        # above the surface instead of sinking through it.
        if hit is not None and y < hit + SKIN:
            y = hit + SKIN

        ever_grounded = ever_grounded or grounded

        # ---- RecoverGrounding() ---- #
        nx, nz = x / 42.0, z / 32.0
        strayed = nx * nx + nz * nz > 0.94
        fell_through = y < -2.5

        if strayed or fell_through:
            x, y, z = last_safe
            vertical_velocity = 0.0
            airborne_time = 0.0
            recoveries += 1
        else:
            ground = sample_height(x, z)
            recoverable = ground > -0.4
            sunken = y < ground - SUNKEN_TOLERANCE
            lost_ground = airborne_time > MAX_AIRBORNE_TIME
            if recoverable and (sunken or lost_ground):
                x, y, z = x, ground + 0.15, z
                vertical_velocity = 0.0
                airborne_time = 0.0
                last_safe = (x, y, z)
                recoveries += 1
            elif grounded:
                last_safe = (x, y, z)

        samples.append(y)

    final_ground = sample_height(x, z)
    return {
        "final_y": y,
        "final_ground": final_ground,
        "recoveries": recoveries,
        "ever_grounded": ever_grounded,
        "samples": samples,
    }


def tail_settled(samples, n=120, tol=0.05):
    tail = samples[-n:]
    return max(tail) - min(tail) < tol


# --------------------------------------------------------------------------- #
# Checks
# --------------------------------------------------------------------------- #


def main() -> int:
    failures, checks = [], []

    def check(name, ok, detail):
        checks.append((name, ok, detail))
        if not ok:
            failures.append(name)

    tri_a, tri_b = read_source_winding()
    offsets = tuple(offset_of(n) for n in tri_a + tri_b)
    print(f"parsed terrain winding from {os.path.relpath(WORLD_BUILDER, REPO)}:")
    print(f"  triangle A = {tri_a}   triangle B = {tri_b}")
    print()

    vertices, triangles = build_island(offsets)

    # 1. WINDING --------------------------------------------------------- #
    faces = face_normals(vertices, triangles)
    up = [n for n in faces if n[1] > 1e-6]
    down = [n for n in faces if n[1] < -1e-6]
    check(
        "winding/all-terrain-triangles-face-up",
        len(up) == len(faces),
        f"{len(up)}/{len(faces)} face up, {len(down)} face down",
    )

    # 2. NORMALS --------------------------------------------------------- #
    normals = recalculate_normals(vertices, triangles)
    normals_up = [n for n in normals if n[1] > 0.0]
    check(
        "normals/recalculated-vertex-normals-point-up",
        len(normals_up) == len(normals),
        f"{len(normals_up)}/{len(normals)} vertex normals point up; "
        f"min y={min(n[1] for n in normals):+.3f}",
    )

    # 3. GROUNDING ------------------------------------------------------- #
    spawn_y = sample_height(SPAWN_X, SPAWN_Z) + SPAWN_Y_OFFSET
    spawn_hit = ground_probe(vertices, triangles, SPAWN_X, SPAWN_Z, spawn_y)
    check(
        "grounding/spawn-probe-finds-ground",
        spawn_hit is not None,
        f"ground probe at spawn ({SPAWN_X}, {SPAWN_Z}) -> "
        + (f"surface at y={spawn_hit:.3f} (feet y={spawn_y:.3f})" if spawn_hit else "MISS"),
    )

    misses, tested = 0, 0
    for gz in range(-26, 27, 4):
        for gx in range(-34, 35, 4):
            nx, nz = gx / 42.0, gz / 32.0
            if nx * nx + nz * nz > 0.94:
                continue  # outside the out-of-bounds guard: not walkable
            h = sample_height(float(gx), float(gz))
            if h < 0.0:
                continue  # beach apron below the tide line: not walkable ground
            tested += 1
            if raycast_down(vertices, triangles, float(gx), h + 2.0, float(gz)) is None:
                misses += 1
    check(
        "grounding/walkable-area-has-upward-ground",
        misses == 0 and tested > 0,
        f"{tested - misses}/{tested} walkable sample points found a front-facing surface",
    )

    # 4. RECOVERY -------------------------------------------------------- #
    # 4a. Standing still on the island stays settled (no creeping or bobbing).
    idle = simulate_player(vertices, triangles, frames=600)
    idle_ok = idle["ever_grounded"] and tail_settled(idle["samples"]) and idle["recoveries"] == 0
    check(
        "recovery/idle-player-stays-grounded",
        idle_ok,
        f"grounded={idle['ever_grounded']}, recoveries={idle['recoveries']} (expect 0), "
        f"tail spread={max(idle['samples'][-120:]) - min(idle['samples'][-120:]):.4f}",
    )

    # 4b. Shoved outside the island bounds: exactly one recovery, then settled.
    shove = simulate_player(vertices, triangles, frames=900, events={300: ("shove", 40.0)})
    shove_err = abs(shove["final_y"] - spawn_y)
    check(
        "recovery/settles-after-out-of-bounds-shove",
        shove["recoveries"] == 1 and tail_settled(shove["samples"]) and shove_err < 0.35,
        f"recoveries={shove['recoveries']} (expect 1), "
        f"tail spread={max(shove['samples'][-120:]) - min(shove['samples'][-120:]):.4f}, "
        f"|final_y - spawn_y|={shove_err:.3f}",
    )

    # 4c. Buried below the terrain surface: the sunken branch catches it and
    #     stands the player back on the ground instead of falling forever.
    buried = simulate_player(vertices, triangles, frames=600, events={30: ("bury", 1.0)})
    buried_ok = (
        buried["recoveries"] >= 1
        and tail_settled(buried["samples"])
        and abs(buried["final_y"] - buried["final_ground"]) < 0.35
        and buried["ever_grounded"]
    )
    check(
        "recovery/recovers-when-buried-below-terrain",
        buried_ok,
        f"recoveries={buried['recoveries']}, finalized on ground "
        f"(|y-ground|={abs(buried['final_y'] - buried['final_ground']):.3f}), "
        f"grounded={buried['ever_grounded']}",
    )

    # 4d. Dropped from a height: lands and settles without spurious recovery.
    dropped = simulate_player(vertices, triangles, frames=600, events={30: ("drop", 3.0)})
    drop_ok = (
        dropped["ever_grounded"]
        and tail_settled(dropped["samples"])
        and abs(dropped["final_y"] - dropped["final_ground"]) < 0.35
    )
    check(
        "recovery/lands-and-settles-after-a-drop",
        drop_ok,
        f"recoveries={dropped['recoveries']}, "
        f"|final_y - ground|={abs(dropped['final_y'] - dropped['final_ground']):.3f}",
    )

    # 5. HEIGHT ---------------------------------------------------------- #
    worst, worst_at = 0.0, None
    for gz in range(-30, 31, 3):
        for gx in range(-40, 41, 3):
            h = sample_height(float(gx), float(gz))
            if h < 0.0:
                continue
            hit = raycast_down(vertices, triangles, float(gx), h + 3.0, float(gz))
            if hit is None:
                continue
            err = abs(hit - h)
            if err > worst:
                worst, worst_at = err, (gx, gz)
    check(
        "height/analytic-SampleHeight-matches-mesh-surface",
        worst < 0.5,
        f"max |SampleHeight - mesh| = {worst:.3f} at xz={worst_at}",
    )

    # ------------------------------------------------ report ----------- #
    width = max(len(name) for name, _, _ in checks)
    print("Tide & Till - terrain winding / player grounding verification")
    print("=" * (width + 50))
    for name, ok, detail in checks:
        print(f"[{'PASS' if ok else 'FAIL'}] {name.ljust(width)}  {detail}")
    print("=" * (width + 50))
    print(f"{len(checks) - len(failures)}/{len(checks)} checks passed")
    if failures:
        print("FAILED: " + ", ".join(failures))
        return 1
    print("All terrain winding and player grounding checks passed.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
