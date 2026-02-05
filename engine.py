import pygame
import colors
import sys
from map import HexGridMap, TileGridMap, IsometricTileGridMap
from viewport import Viewport
from minimap import Minimap
from unit import Unit
from input_manager import InputManager


class GameEngine:

    def __init__(self, width, height, fullscreen=False, move_speed=0.5, zoom_speed=0.5):
        self.width = width
        self.height = height
        self.fullscreen = fullscreen
        self.screen = pygame.display.set_mode((width, height))
        if fullscreen:
            pygame.display.toggle_fullscreen()
        self.done = False
        pygame.mouse.set_visible(False)
        self.map = HexGridMap(21, 11, hex_radius=100, hex_vertical_scale=0.9)
        self.map.tiles[10][10].unit = Unit()
        #self.map = TileGridMap(30, 30, tile_width=80, tile_height=120)
        #self.map = IsometricTileGridMap(20, 20, tile_width=60, tile_height=40)

        minimap_width = width / 5
        minimap_height = height / 5

        self.viewport = Viewport(screen_x1=0,
                                 screen_y1=0,
                                 screen_width=width,
                                 screen_height=height,
                                 zoom_level=1,
                                 map=self.map,
                                 minimap=None,
                                 can_zoom=False,
                                 can_move=False,
                                 zoom_speed=10,
                                 is_minimap=False,
                                 is_primary=True)

        self.minimap = Minimap(screen_x1=width - minimap_width,
                               screen_y1=height - minimap_height,
                               screen_width=minimap_width,
                               screen_height=minimap_height,
                               zoom_level=1,
                               map=self.map,
                               primary_viewport=self.viewport,
                               zoom_speed=10)

        self.viewport.minimap = self.minimap

        self.input_manager = InputManager()
        self.move_speed = move_speed
        self.zoom_speed = zoom_speed

    def run(self, fps):
        clock = pygame.time.Clock()
        while not self.done:
            ms = clock.get_time()
            seconds = ms / 1000.0
            events = pygame.event.get()
            mouse = pygame.mouse.get_pos()
            self.update(events, mouse, seconds)
            self.draw(events, mouse)
            clock.tick(fps)

    def update(self, events, mouse, seconds):
        # Handle quit events
        for event in events:
            if event.type == pygame.QUIT:
                pygame.quit()
                sys.exit()
        
        # Update input manager
        self.input_manager.update(events, mouse)
        
        # Update viewport with all input state
        self.viewport.update(seconds, 
                           mouse_pos=self.input_manager.mouse_pos,
                           mouse_down=self.input_manager.mouse_down,
                           mouse_released=self.input_manager.mouse_released,
                           direction_x=self.input_manager.direction_x,
                           direction_y=self.input_manager.direction_y,
                           zoom_direction=self.input_manager.zoom_direction,
                           move_speed=self.move_speed,
                           zoom_speed=self.zoom_speed)

    def draw(self, events, mouse):
        self.screen.fill(colors.BLACK)
        self.viewport.draw()
        self.screen.blit(self.viewport.surface, (self.viewport.screen_x1, self.viewport.screen_y1))
        if self.minimap is not None:
            self.minimap.draw()
            self.screen.blit(self.minimap.surface, (self.minimap.screen_x1, self.minimap.screen_y1))
        pygame.draw.rect(self.screen, (255, 255, 255), [mouse[0]-5, mouse[1]-5, 10, 10])
        pygame.display.flip()

