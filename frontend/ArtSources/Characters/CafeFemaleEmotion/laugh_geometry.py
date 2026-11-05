"""카페 검수 얼굴의 폭소 접합선을 다듬는 국소 메시 변형."""

import numpy as np
from mathutils import Vector
from mathutils.bvhtree import BVHTree


def round_boundary(vertices, normals, triangles, lip_aa, depth):
    count = len(lip_aa) // 6
    unique, inverse = np.unique(np.round(vertices, 6), axis=0, return_inverse=True)
    unique_triangles = inverse[triangles]
    boundary = lip_aa[:count:4]
    boundary_ids = np.array(
        [np.argmin(np.linalg.norm(unique - point, axis=1)) for point in boundary]
    )
    angle = np.unwrap(
        np.arctan2(
            (boundary[:, 2] - 0.477) / 0.028,
            (boundary[:, 1] + 0.043) / 0.055,
        )
    )
    steps = np.arange(len(angle)) * 0.025
    angle = np.maximum.accumulate(angle - steps) + steps
    destination = np.column_stack(
        [-0.043 + 0.065 * np.cos(angle), 0.474 + 0.035 * np.sin(angle)]
    )
    # Retain some of the cut's original contour to avoid folding adjacent body
    # triangles. Free vertices average neighboring offsets within a fixed rim.
    fixed_delta = (destination - boundary[:, 1:]) * 0.85
    movable = (
        (unique[:, 0] < -0.18)
        & (abs(unique[:, 1] + 0.043) < 0.16)
        & (unique[:, 2] > 0.39)
        & (unique[:, 2] < 0.508)
    )
    movable[boundary_ids] = False
    free = np.flatnonzero(movable)
    lookup = {vertex: row for row, vertex in enumerate(free)}
    fixed = dict(zip(boundary_ids, fixed_delta))
    neighbors = [set() for _ in unique]
    for triangle in unique_triangles:
        for vertex in triangle:
            neighbors[vertex].update(set(triangle) - {vertex})
    matrix = np.zeros((len(free), len(free)))
    right = np.zeros((len(free), 2))
    for row, vertex in enumerate(free):
        for neighbor in neighbors[vertex]:
            weight = 1 / max(np.linalg.norm(unique[vertex] - unique[neighbor]), 1e-5)
            matrix[row, row] += weight
            if neighbor in lookup:
                matrix[row, lookup[neighbor]] -= weight
            elif neighbor in fixed:
                right[row] += weight * fixed[neighbor]
        if matrix[row, row] == 0:
            matrix[row, row] = 1
    unique_delta = np.zeros((len(unique), 2))
    unique_delta[free] = np.linalg.solve(matrix, right)
    unique_delta[boundary_ids] = fixed_delta
    delta = np.zeros_like(vertices)
    delta[:, 1:] = unique_delta[inverse]
    u, v = vertices[:, 1] + 0.043, vertices[:, 2] - 0.477
    delta[:, 0] = depth(u + delta[:, 1], v + delta[:, 2]) - depth(u, v)

    moved = vertices + delta
    u, v = moved[:, 1] + 0.043, moved[:, 2] - 0.477
    epsilon = 1e-5
    slope_u = (depth(u + epsilon, v) - depth(u - epsilon, v)) / (2 * epsilon)
    slope_v = (depth(u, v + epsilon) - depth(u, v - epsilon)) / (2 * epsilon)
    smooth = np.column_stack([-np.ones(len(moved)), slope_u, slope_v])
    smooth /= np.linalg.norm(smooth, axis=1, keepdims=True)
    radius = np.sqrt(
        ((vertices[:, 1] + 0.043) / 0.15) ** 2 + ((vertices[:, 2] - 0.477) / 0.10) ** 2
    )
    blend = np.clip((1.25 - radius) / 0.4, 0, 1)
    blend *= np.clip((-0.14 - vertices[:, 0]) / 0.06, 0, 1)
    blend *= np.clip((0.520 - vertices[:, 2]) / 0.020, 0, 1)
    # Keep the protruding nose's authored shading instead of flattening it
    # into the fitted cheek surface during the laugh.
    nose = np.clip((0.040 - abs(vertices[:, 1] + 0.043)) / 0.015, 0, 1)
    nose *= np.clip((vertices[:, 2] - 0.489) / 0.015, 0, 1)
    blend *= 1 - nose
    blend = blend * blend * (3 - 2 * blend)
    turned = normals * (1 - blend[:, None]) + smooth * blend[:, None]
    turned /= np.maximum(np.linalg.norm(turned, axis=1, keepdims=True), 1e-10)
    rim_angle = np.interp(
        np.arange(count) / 4,
        np.arange(len(angle) + 1),
        np.r_[angle, angle[0] + 2 * np.pi],
    )
    return delta, turned - normals, rim_angle


def bind_surface(vertices, triangles, delta, normal_delta):
    tree = BVHTree.FromPolygons(
        vertices.tolist(), triangles.tolist(), all_triangles=True
    )

    def follow(points):
        offsets = np.zeros_like(points)
        normals = np.zeros_like(points)
        for index, point in enumerate(points):
            hit, _, face, distance = tree.find_nearest(Vector(point))
            if hit is None or distance > 0.025:
                continue
            ids = triangles[face]
            a, b, c = vertices[ids]
            first, second, offset = b - a, c - a, np.array(hit) - a
            d00 = np.dot(first, first)
            d01 = np.dot(first, second)
            d11 = np.dot(second, second)
            d20 = np.dot(offset, first)
            d21 = np.dot(offset, second)
            denominator = d00 * d11 - d01 * d01
            if abs(denominator) < 1e-18:
                continue
            w1 = (d11 * d20 - d01 * d21) / denominator
            w2 = (d00 * d21 - d01 * d20) / denominator
            weights = np.array([1 - w1 - w2, w1, w2])
            offsets[index] = weights @ delta[ids]
            normals[index] = weights @ normal_delta[ids]
        return offsets, normals

    return follow
