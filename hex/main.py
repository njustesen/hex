import pygame
from hex import colors
from hex.engine import GameEngine
from hex.engine_config import EngineConfig


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

    # Initialize the engine config
    #EngineConfig.initialize(width=screen_width, height=screen_height, fullscreen=False)
    engine = GameEngine()

    engine.run(fps=60)
