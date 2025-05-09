import pygame
import colors
from engine import GameEngine


if __name__ == '__main__':

    pygame.init()
    pygame.mixer.init()
    pygame.font.init()
    pygame.mouse.set_visible(False)

    display_info = pygame.display.Info()
    screen_width = display_info.current_w / 2
    screen_height = display_info.current_h / 2

    screen = pygame.display.set_mode((screen_width, screen_height))
    num_players = 1

    engine = GameEngine(screen_width, screen_height, fullscreen=False)

    engine.run(fps=60)
