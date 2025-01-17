import pygame
from hexagon import Hexagon
import colors
import math
import sys


class Map:

    def __init__(self, height, width, hex_radius, hex_vertical_scale):
        self.hex_radius = hex_radius  # Radius of the circumscribed circle of a hexagon
        self.hex_vertical_scale = hex_vertical_scale  # Scale factor for hexagon height (1.0 = regular hexagon)
        self.hex_height = self.hex_vertical_scale * math.sqrt(3) * self.hex_radius
        self.hex_width = 2 * self.hex_radius
        self.horizontal_spacing = self.hex_width * 3 / 4
        self.vertical_spacing = self.hex_height
        self.height = height
        self.width = width
        self.font = pygame.font.SysFont('Comic Sans MS', 30)
        self.hexes = []
        for y in range(height):
            row = []
            for x in range(width):
                x_offset = x * self.horizontal_spacing
                y_offset = y * self.vertical_spacing
                if x % 2 == 1:
                    y_offset += self.vertical_spacing / 2
                pos = (x_offset, y_offset)
                row.append(Hexagon(pos, x, y, self.hex_radius, self.hex_vertical_scale, self.hex_height, self.hex_width))
            self.hexes.append(row)

    def draw(self, surface, hover=None, debug=False):
        for y in range(self.height):
            for x in range(self.width):
                hex = self.hexes[y][x]
                if hex is hover:
                    pygame.draw.polygon(surface, colors.GREEN, hex.points)
                pygame.draw.polygon(surface, colors.GREEN, hex.points, 1)
                if debug:
                    pygame.draw.circle(surface, color=colors.GREEN, center=hex.pos, radius=4, width=1)
                    self.draw_text_on_screen(surface, str(f"{x}, {y}"), x=hex.pos[0], y=hex.pos[1], color=colors.GREEN)


    def draw_text_on_screen(self, surface, text, x, y, color):
        rendered_text = self.font.render(text, True, color)
        w = rendered_text.get_width()
        h = rendered_text.get_height()
        position = (x - w/2, y - h/2, w, h)
        surface.blit(rendered_text, position)

    def get_nearest_hex(self, pos):
        min_distance = sys.maxsize
        nearest_hex = None
        for y in range(self.height):
            for x in range(self.width):
                hex = self.hexes[y][x]
                distance = math.dist(hex.pos, pos)
                if distance < min_distance:
                    min_distance = distance
                    nearest_hex = hex
        return nearest_hex
