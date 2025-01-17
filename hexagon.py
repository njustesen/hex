import math


class Hexagon:

    def __init__(self, pos, x, y, radius, vertical_scale, width, height):
        self.pos = pos
        self.x = x
        self.y = y
        self.width = width
        self.vertical_scale = vertical_scale
        self.height = height
        self.radius = radius
        self.points = self._compute_points()

    def _compute_points(self):
        cx, cy = self.pos
        return [
            (cx + self.radius * math.cos(math.radians(angle)),
             cy + (self.radius * self.vertical_scale) * math.sin(math.radians(angle)))
            for angle in range(0, 360, 60)
        ]
