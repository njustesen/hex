import pygame
import math

# Initialize pygame
pygame.init()

# Screen dimensions
WIDTH, HEIGHT = 800, 600
screen = pygame.display.set_mode((WIDTH, HEIGHT))
pygame.display.set_caption("Hexagon Grid")

# Colors
WHITE = (255, 255, 255)
BLACK = (0, 0, 0)

# Hexagon properties
HEX_RADIUS = 30  # Radius of the circumscribed circle of a hexagon
HEX_VERTICAL_SCALE = 0.7  # Scale factor for hexagon height (1.0 = regular hexagon)
HEX_HEIGHT = HEX_VERTICAL_SCALE * math.sqrt(3) * HEX_RADIUS
HEX_WIDTH = 2 * HEX_RADIUS
HORIZONTAL_SPACING = HEX_WIDTH * 3/4
VERTICAL_SPACING = HEX_HEIGHT

def draw_hexagon(surface, color, center):
    """Draws a hexagon centered at the given position."""
    cx, cy = center
    points = [
        (cx + HEX_RADIUS * math.cos(math.radians(angle)),
         cy + (HEX_RADIUS * HEX_VERTICAL_SCALE) * math.sin(math.radians(angle)))
        for angle in range(0, 360, 60)
    ]
    pygame.draw.polygon(surface, color, points, width=1)

def draw_hexagon_grid():
    """Draws a grid of hexagons."""
    for row in range(HEIGHT // int(VERTICAL_SPACING) + 2):
        for col in range(WIDTH // int(HORIZONTAL_SPACING) + 2):
            # Calculate the x and y offset for the hexagon
            x_offset = col * HORIZONTAL_SPACING
            y_offset = row * VERTICAL_SPACING

            # Offset every other row to create the hexagonal tiling pattern
            if col % 2 == 1:
                y_offset += VERTICAL_SPACING / 2

            # Only draw hexagons that are inside the screen
            if x_offset - HEX_RADIUS < WIDTH and y_offset - HEX_RADIUS < HEIGHT:
                draw_hexagon(screen, WHITE, (x_offset, y_offset))

def main():
    clock = pygame.time.Clock()
    running = True

    while running:
        for event in pygame.event.get():
            if event.type == pygame.QUIT:
                running = False

        screen.fill(BLACK)
        draw_hexagon_grid()
        pygame.display.flip()
        clock.tick(60)

    pygame.quit()

if __name__ == "__main__":
    main()
