import pygame
import colors
import math
import sys
from tile import Hexagon, SquareTile, IsometricTile


class GridMap:

    def __init__(self, rows, cols):
        self.rows = rows
        self.cols = cols
        self.tiles = []
        self.font = pygame.font.SysFont('Comic Sans MS', 30)
        self.vertical_spacing = None
        self.horizontal_spacing = None

    def init(self, vertical_spacing, horizontal_spacing):
        self.vertical_spacing = vertical_spacing
        self.horizontal_spacing = horizontal_spacing

    @property
    def rect(self):
        return pygame.Rect(self.x1, self.y1, self.width, self.height)

    @property
    def width(self):
        return self.horizontal_spacing * self.cols

    @property
    def height(self):
        return self.vertical_spacing * self.rows

    @property
    def x1(self):
        return -self.width / 2

    @property
    def y1(self):
        return -self.height / 2

    @property
    def center(self):
        return self.x1 + self.width / 2, self.y1 + self.height / 2

    def draw(self, surface, hover=None, debug=False):
        for y in range(self.rows):
            for x in range(self.cols):
                tile = self.tiles[y][x]
                if tile is hover:
                    pygame.draw.polygon(surface, (0, 80, 0), tile.points)
                pygame.draw.polygon(surface, (0, 80, 0), tile.points, 1)
                if debug:
                    pygame.draw.circle(surface, color=colors.GREEN, center=tile.pos, radius=4, width=1)
                    self.draw_text_on_screen(surface, str(f"{x}, {y}"), x=tile.pos[0], y=tile.pos[1], color=colors.GREEN)


    def draw_text_on_screen(self, surface, text, x, y, color):
        rendered_text = self.font.render(text, True, color)
        w = rendered_text.get_width()
        h = rendered_text.get_height()
        position = (x - w/2, y - h/2, w, h)
        surface.blit(rendered_text, position)

    def get_nearest_tile(self, pos):
        raise NotImplementedError()


class HexGridMap(GridMap):

    def __init__(self, rows, cols, hex_radius, hex_vertical_scale, hex_orientation="flat"):
        super().__init__(rows, cols)
        self.hex_radius = hex_radius  # Radius of the circumscribed circle of a hexagon
        self.hex_vertical_scale = hex_vertical_scale  # Scale factor for hexagon height (1.0 = regular hexagon)
        self.hex_orientation = hex_orientation
        if hex_orientation == "flat":
            self.hex_width = 2 * self.hex_radius
            self.hex_height = self.hex_vertical_scale * math.sqrt(3) * self.hex_radius
            self.horizontal_spacing = 3 / 4 * self.hex_width
            self.vertical_spacing = self.hex_height
        elif hex_orientation == "pointy":
            self.hex_width = self.hex_vertical_scale * math.sqrt(3) * self.hex_radius
            self.hex_height = 2 * self.hex_radius
            self.horizontal_spacing = self.hex_width
            self.vertical_spacing = 3 / 4 * self.hex_height
        self.generate()

    def generate(self):
        for y in range(self.rows):
            row = []
            for x in range(self.cols):
                x_offset = x * self.horizontal_spacing
                y_offset = y * self.vertical_spacing
                if self.hex_orientation == "flat" and x % 2 == 1:
                    y_offset += self.vertical_spacing / 2
                elif self.hex_orientation == "pointy" and y % 2 == 1:
                    x_offset += self.horizontal_spacing / 2
                pos = (x_offset, y_offset)
                row.append(Hexagon(pos, x, y, self.hex_radius, self.hex_vertical_scale, self.hex_height, self.hex_width, self.hex_orientation))
            self.tiles.append(row)
        self.init(self.vertical_spacing, self.horizontal_spacing)

    def get_nearest_tile(self, pos):
        upscale = 1 / self.hex_vertical_scale
        scaled_pos = (pos[0], pos[1] * upscale)
        min_distance = sys.maxsize
        nearest_tile = None
        for y in range(self.rows):
            for x in range(self.cols):
                tile = self.tiles[y][x]
                scaled_tile_pos = (tile.pos[0], tile.pos[1] * upscale)
                distance = math.dist(scaled_tile_pos, scaled_pos)
                if distance < min_distance:
                    min_distance = distance
                    nearest_tile = tile
        return nearest_tile


class TileGridMap(GridMap):

    def __init__(self, rows, cols, tile_width, tile_height):
        super().__init__(rows, cols)
        self.tile_height = tile_height
        self.tile_width = tile_width
        self.generate()

    def generate(self):
        for y in range(self.rows):
            row = []
            for x in range(self.cols):
                x_offset = x * self.tile_width
                y_offset = y * self.tile_height
                pos = (x_offset, y_offset)
                row.append(SquareTile(pos, x, y, self.tile_width, self.tile_height))
            self.tiles.append(row)
        self.init(self.vertical_spacing, self.horizontal_spacing)

    def get_nearest_tile(self, pos):
        x_idx = int((pos[0] + self.tile_width / 2) / self.tile_width)
        y_idx = int((pos[1] + self.tile_height / 2) / self.tile_height)
        return self.tiles[y_idx][x_idx]


class IsometricTileGridMap(GridMap):

    def __init__(self, rows, cols, tile_width, tile_height):
        super().__init__(rows, cols)
        self.tile_height = tile_height
        self.tile_width = tile_width
        self.vertical_spacing = tile_height
        self.horizontal_spacing = tile_width
        self.generate()

    def generate(self):
        for y in range(self.rows):
            row = []
            for x in range(self.cols):
                x_offset = ((self.cols * self.tile_width) / 2) + (x - y) * (self.tile_width / 2)
                y_offset = (self.tile_height / 2) + (x + y) * (self.tile_height / 2)
                pos = (x_offset, y_offset)
                row.append(IsometricTile(pos, x, y, self.tile_width, self.tile_height))
            self.tiles.append(row)
        self.init(self.vertical_spacing, self.horizontal_spacing)

    def get_nearest_tile(self, pos):
        upscale = self.tile_width / self.tile_height
        scaled_pos = (pos[0], pos[1] * upscale)
        min_distance = sys.maxsize
        nearest_tile = None
        for y in range(self.rows):
            for x in range(self.cols):
                tile = self.tiles[y][x]
                scaled_tile_pos = (tile.pos[0], tile.pos[1] * upscale)
                distance = math.dist(scaled_tile_pos, scaled_pos)
                if distance < min_distance:
                    min_distance = distance
                    nearest_tile = tile
        return nearest_tile
