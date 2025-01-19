import math


class Tile:

    def __init__(self, pos, x, y, width, height):
        self.pos = pos
        self.x = x
        self.y = y
        self.width = width
        self.height = height
        self.points = []


class Hexagon(Tile):

    def __init__(self, pos, x, y, radius, vertical_scale, width, height, orientation="pointy"):
        super().__init__(pos, x, y, width, height)
        self.vertical_scale = vertical_scale
        self.radius = radius
        self.orientation = orientation
        self.points = self._compute_points()

    def _compute_points(self):
        cx, cy = self.pos
        if self.orientation == "flat":
            angles = range(0, 360, 60)  # Flat-topped: flat sides at top/bottom
        else:
            angles = [30 + i * 60 for i in range(6)]  # Pointy-topped: points at top/bottom
        return [
            (cx + self.radius * math.cos(math.radians(angle)),
             cy + (self.radius * self.vertical_scale) * math.sin(math.radians(angle)))
            for angle in angles
        ]


class SquareTile(Tile):

    def __init__(self, pos, x, y, width, height):
        super().__init__(pos, x, y, width, height)
        self.points = [
            (pos[0] - width / 2, pos[1] - height / 2),
            (pos[0] + width / 2, pos[1] - height / 2),
            (pos[0] + width / 2, pos[1] + height / 2),
            (pos[0] - width / 2, pos[1] + height / 2),
            (pos[0] - width / 2, pos[1] - height / 2)
        ]


class IsometricTile(Tile):

    def __init__(self, pos, x, y, width, height):
        super().__init__(pos, x, y, width, height)
        # Define the four corners of the diamond-shaped isometric tile
        self.points = [
            (pos[0], pos[1] - height / 2),        # Top point
            (pos[0] + width / 2, pos[1]),         # Right point
            (pos[0], pos[1] + height / 2),        # Bottom point
            (pos[0] - width / 2, pos[1]),         # Left point
            (pos[0], pos[1] - height / 2)         # Back to top point (to close the shape)
        ]
