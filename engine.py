import pygame
import colors
import sys
from map import HexGridMap, TileGridMap, IsometricTileGridMap, IsometricHexGridMap
from viewport import Viewport


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
        # self.map = HexGridMap(21, 11, hex_radius=100, hex_vertical_scale=0.9)
        self.map = IsometricHexGridMap(21, 11, hex_radius=100, hex_vertical_scale=1)
        #self.map = TileGridMap(30, 30, tile_width=80, tile_height=120)
        #self.map = IsometricTileGridMap(20, 20, tile_width=60, tile_height=40)
        self.hover_tile = None

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
                                 viewports=None,
                                 is_minimap=False,
                                 is_primary=False)
        self.keys_down = set()
        self.keys_pressed = set()
        self.keys_released = set()
        self.zoom_direction = 0
        self.direction_x = 0
        self.direction_y = 0
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
        self._update_keys(events)
        for event in events:
            if event.type == pygame.QUIT:
                pygame.quit()
                sys.exit()
        self.viewport.zoom_cam(self.zoom_direction * self.zoom_speed * seconds)
        self.move_cam(seconds)
        mouse_position = self.viewport.surface_to_world(mouse)
        self.viewport.hover_tile = self.map.get_nearest_tile(mouse_position)

    def _update_keys(self, events):
        self.keys_pressed.clear()
        self.keys_released.clear()
        self.zoom_direction = 0
        for event in events:
            if event.type == pygame.QUIT:
                pygame.quit()
                sys.exit()
            if event.type == pygame.KEYDOWN:
                if event.key not in self.keys_down:
                    self.keys_pressed.add(event.key)
                self.keys_down.add(event.key)
            if event.type == pygame.KEYUP:
                self.keys_released.add(event.key)
                if event.key in self.keys_down:
                    self.keys_down.remove(event.key)
            if event.type == pygame.MOUSEWHEEL:
                self.zoom_direction = event.y
        self.direction_x = 0
        self.direction_y = 0
        if pygame.K_LEFT in self.keys_down or pygame.K_a in self.keys_down:
            self.direction_x -= 1
        if pygame.K_RIGHT in self.keys_down or pygame.K_d in self.keys_down:
            self.direction_x += 1
        if pygame.K_UP in self.keys_down or pygame.K_w in self.keys_down:
            self.direction_y -= 1
        if pygame.K_DOWN in self.keys_down or pygame.K_s in self.keys_down:
            self.direction_y += 1

    def move_cam(self, seconds):
        speed = seconds * self.viewport.cam.width * self.move_speed
        if self.direction_x != 0 or self.direction_y != 0:
            self.viewport.move_cam((self.direction_x * speed, self.direction_y * speed))

    def draw(self, events, mouse):
        self.screen.fill(colors.BLACK)
        self.viewport.draw()
        self.screen.blit(self.viewport.surface, (self.viewport.screen_x1, self.viewport.screen_y1))
        pygame.draw.rect(self.screen, (255, 255, 255), [mouse[0]-5, mouse[1]-5, 10, 10])
        pygame.display.flip()

