import pygame
import colors
import sys
from map import HexGridMap, TileGridMap, IsometricTileGridMap
from viewport import Viewport
from minimap import Minimap
from unit import Unit


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
        self.hover_tile = None

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

        self.keys_down = set()
        self.keys_pressed = set()
        self.keys_released = set()
        self.mouse_down = False
        self.mouse_released = False
        self.prev_mouse_pos = None
        self.dragging = False
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
        self._handle_drag_panning(mouse)
        self.viewport.zoom_cam(self.zoom_direction * self.zoom_speed * seconds)
        self.move_cam(seconds)
        mouse_position = self.viewport.surface_to_world(mouse)
        self.viewport.hover_tile = self.map.get_nearest_tile(mouse_position)
        if self.mouse_released and not self.dragging:
            self.viewport.selected_tile = self.viewport.hover_tile
        self.viewport.update(seconds)

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
        mouse_down = pygame.mouse.get_pressed()[0]
        self.mouse_released = self.mouse_down and not mouse_down
        self.mouse_down = mouse_down

    def _handle_drag_panning(self, mouse):
        """Handle mouse drag panning of the camera."""
        surface = self.get_surface(mouse[0], mouse[1])
        if surface is None:
            return
        if self.mouse_down:
            # If dragging
            if self.prev_mouse_surface is not None:
                if self.prev_mouse_surface.is_primary:
                    # Calculate mouse movement
                    dx = mouse[0] - self.prev_mouse_pos[0]
                    dy = mouse[1] - self.prev_mouse_pos[1]
                    
                    # Start dragging if there's significant movement
                    if not self.dragging and (abs(dx) > 2 or abs(dy) > 2):
                        self.dragging = True
                    
                    # Update camera position if dragging
                    if self.dragging:
                        self.viewport.pan_drag((dx, dy))
                elif self.prev_mouse_surface.is_minimap:
                    self.minimap_pan()
            self.prev_mouse_pos = mouse
            if self.prev_mouse_surface is None and surface.is_minimap:
                self.minimap_pan()
            self.prev_mouse_surface = surface
            
        else:
            # Mouse released or not down
            if self.mouse_released:
                self.dragging = False
            self.prev_mouse_pos = None
            self.prev_mouse_surface = None

    def minimap_pan(self):
        pos = self.minimap.mouse_on_surface()
        if pos is None:
            return
        surface_x1, surface_y1 = pos
        world_x1, world_y1 = self.minimap.surface_to_world((surface_x1, surface_y1))
        self.viewport.cam.change(center=(world_x1, world_y1), map=self.viewport.map)
        self.viewport.moved = True

    def get_surface(self, x, y):
        if self.minimap is not None:
            if self.minimap.is_within(x, y):
                return self.minimap
        if self.viewport.is_within(x, y):
            return self.viewport
        return None
    
    def move_cam(self, seconds):
        speed = seconds * self.viewport.cam.width * self.move_speed
        if self.direction_x != 0 or self.direction_y != 0:
            self.viewport.move_cam((self.direction_x * speed, self.direction_y * speed))

    def draw(self, events, mouse):
        self.screen.fill(colors.BLACK)
        self.viewport.draw()
        self.screen.blit(self.viewport.surface, (self.viewport.screen_x1, self.viewport.screen_y1))
        if self.minimap is not None:
            self.minimap.draw()
            self.screen.blit(self.minimap.surface, (self.minimap.screen_x1, self.minimap.screen_y1))
        pygame.draw.rect(self.screen, (255, 255, 255), [mouse[0]-5, mouse[1]-5, 10, 10])
        pygame.display.flip()

