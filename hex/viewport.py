import pygame
from hex import colors
import math
from hex.map import GridMap
from hex.camera import Camera
from hex.engine_config import EngineConfig


class Viewport:

    def __init__(self,
                 screen_x1,
                 screen_y1,
                 screen_width,
                 screen_height,
                 zoom_level,
                 map: GridMap,
                 minimap=None,
                 can_zoom=False,
                 can_move=False,
                 color=colors.BLACK,
                 primary_viewport=None,
                 is_minimap=False,
                 is_primary=False):
        self.screen_x1 = screen_x1
        self.screen_y1 = screen_y1
        self.screen_width = screen_width
        self.screen_height = screen_height
        self.screen_ratio = screen_width / screen_height
        self.color = color
        self.map = map
        self.minimap = minimap
        self.is_primary = is_primary
        self.is_minimap = is_minimap
        self.primary_viewport = primary_viewport
        self.can_zoom = can_zoom
        self.can_move = can_move
        self.upp_width = map.width / screen_width
        self.upp_height = map.height / screen_height
        if self.upp_width > self.upp_height:
            self.upp = self.upp_height  # Stretch world to width
        else:
            self.upp = self.upp_width  # Stretch world to height
        self.ppu = 1 / self.upp
        self.bounds = self.map.rect
        self.map_ratio = map.width / map.height
        if self.is_minimap:
            if self.map_ratio > self.screen_ratio:
                # Map is wider than viewport -> show entire width of map
                self.cam = Camera(self.map.center, map.width, map.width * (screen_height/screen_width))
            else:
                # Map is taller than viewport -> show entire height of map
                self.cam = Camera(self.map.center, map.height * (screen_width/screen_height), map.height)
        else:
            if self.map_ratio > self.screen_ratio:
                # Map is wider than viewport -> show entire height of map and crop width
                bounds = pygame.Rect(self.map.x1, self.map.y1, map.width, map.height)
                self.cam = Camera(self.map.center, map.height * (screen_width/screen_height), map.height, bounds=bounds)
            else:
                # Map is taller than viewport -> show entire width of map and crop height
                bounds = pygame.Rect(self.map.x1, self.map.y1, map.width, map.height)
                self.cam = Camera(self.map.center, map.width, map.width * (screen_height/screen_width), bounds=self.bounds)
        self.zoom_level = zoom_level
        self.moved = False
        self.cropped_minimap = None
        self.surface = pygame.Surface((screen_width, screen_height), pygame.SRCALPHA)
        self.hover_tile = None
        self.selected_tile = None
        self.dragging = False
        self.prev_mouse_pos = None
        self.prev_mouse_surface = None

    def center_cam(self, position):
        self.cam.set_center(position, bounds=self.bounds)

    def world_to_surface(self, world_position):
        x_norm_cam, y_norm_cam = self.cam.norm(world_position)
        x_viewport = x_norm_cam * self.screen_width
        y_viewport = y_norm_cam * self.screen_height
        return x_viewport, y_viewport

    def surface_to_world(self, surface_position):
        norm_x = (surface_position[0] - self.screen_x1) / self.screen_width
        norm_y = (surface_position[1] - self.screen_y1) / self.screen_height
        x_world = self.cam.x1 + self.cam.width * norm_x
        y_world = self.cam.y1 + self.cam.height * norm_y
        return x_world, y_world

    def draw(self, grid=None):
        self.surface.fill(self.color)
        if grid is not None:
            self._draw_grid(grid)
        self.draw_map()
        if self.minimap is not None:
            self.minimap.draw()

    def draw_map(self, debug=False):
        for y in range(self.map.rows):
            for x in range(self.map.cols):
                tile = self.map.tiles[y][x]
                tile_screen_points = [self.world_to_surface(point) for point in tile.points]
                tile_screen_position = self.world_to_surface(tile.pos)
                if tile is self.selected_tile:
                    if tile is self.hover_tile:
                        pygame.draw.polygon(self.surface, (80, 160, 80), tile_screen_points)
                    else:
                        pygame.draw.polygon(self.surface, (40, 120, 40), tile_screen_points)
                elif tile is self.hover_tile:
                    pygame.draw.polygon(self.surface, (0, 80, 0), tile_screen_points)
                pygame.draw.polygon(self.surface, (0, 80, 0), tile_screen_points, 1)
                if tile.unit:
                    unit_size = 50
                    unit_world_points = [
                        (tile.pos[0] - unit_size / 2, tile.pos[1] - unit_size / 2),
                        (tile.pos[0] + unit_size / 2, tile.pos[1] - unit_size / 2),
                        (tile.pos[0] + unit_size / 2, tile.pos[1] + unit_size / 2),
                        (tile.pos[0] - unit_size / 2, tile.pos[1] + unit_size / 2)
                    ]
                    unit_screen_points = [self.world_to_surface(point) for point in unit_world_points]
                    pygame.draw.polygon(self.surface, (200, 0, 0), unit_screen_points)
                if debug:
                    pygame.draw.circle(self.surface, color=colors.GREEN, center=tile_screen_position, radius=4*self.scale, width=1)
                    self.draw_text_on_screen(self.surface, str(f"{x}, {y}"), screen_position=tile_screen_position, color=colors.GREEN)

    def _draw_grid(self, grid):
        hori_lines = []
        vert_lines = []
        y_offset = self.map.y1 % float(grid)
        x_offset = self.map.x1 % float(grid)
        for y_idx in range(0, int(self.map.height / grid)):
            hori_lines.append(self.map.y1 + y_offset + y_idx * grid)
        for x_idx in range(0, int(self.map.width / grid)):
            vert_lines.append(self.map.x1 + x_offset + x_idx * grid)
        for y_world in hori_lines:
            point_start = self.world_to_surface((self.map.x1, y_world))
            point_end = self.world_to_surface((self.map.x1 + self.map.width, y_world))
            pygame.draw.line(self.surface, color=colors.GREY, start_pos=point_start, end_pos=point_end)
        for x_world in vert_lines:
            point_start = self.world_to_surface((x_world, self.map.y1))
            point_end = self.world_to_surface((x_world, self.map.y1 + self.map.height))
            pygame.draw.line(self.surface, color=colors.GREY, start_pos=point_start, end_pos=point_end)

    @property
    def scale(self):
        return self.ppu / self.zoom_level

    def draw_text_in_world(self, text, size, world_position, color):
        color = colors.named_colors[color] if color else None
        screen_pos = self.world_to_surface(world_position)
        if color is not None:
            fontsize = int(self.scale * size)
            font = pygame.font.Font(None, fontsize)
            rendered_text = font.render(text, True, color)
            w = rendered_text.get_width()
            h = rendered_text.get_height()
            position = (screen_pos[0] - w/2, screen_pos[1] - h/2, w, h)
            self.surface.blit(rendered_text, position)

    def draw_text_on_screen(self, text, fontsize, screen_position, color):
        font = pygame.font.Font(None, fontsize)
        rendered_text = font.render(text, True, color)
        w = rendered_text.get_width()
        h = rendered_text.get_height()
        position = (screen_position[0] - w/2, screen_position[1] - h/2, w, h)
        self.surface.blit(rendered_text, position)

    def _draw_circle(self, radius, position, color, size_ratio):
        screen_pos = self.world_to_surface(position)
        radius_screen = max(1, radius * size_ratio)
        pygame.draw.circle(self.surface,
                           center=(screen_pos[0], screen_pos[1]),
                           radius=radius_screen,
                           color=color)

    def _get_anchor_point(self, local_anchor, size_ratio):
        x = self.screen_width / 2
        y = self.screen_height / 2
        if local_anchor is None or local_anchor == 'center':
            return x * size_ratio, y * size_ratio
        if 'left' in local_anchor:
            x = 0
        if 'right' in local_anchor:
            x = self.screen_width
        if 'top' in local_anchor:
            y = 0
        if 'bottom' in local_anchor:
            y = self.screen_height
        return x * size_ratio, y * size_ratio

    def _draw_rectangle_in_world(self, width, height, world_position, color, edgecolor, size_ratio, rotation, local_anchor):
        position = self.world_to_surface(world_position)
        width = width * size_ratio
        height = height * size_ratio
        anchor = self._get_anchor_point(local_anchor, size_ratio)

        if rotation == 0:
            pygame.draw.rect(self.surface, rect=pygame.Rect(position[0] - width / 2, position[1] - height / 2, width, height), color=color)
        else:

            # Create a surface for the rectangle with transparency
            rect_surface = pygame.Surface((width, height), pygame.SRCALPHA)
            if color is not None:
                rect_surface.fill(color)

            # Rotate the rectangle surface around its center
            rotated_surface = pygame.transform.rotate(rect_surface, -rotation)

            # Calculate the offset for the anchor point after rotation
            rotated_rect = rotated_surface.get_rect()

            # Determine the position of the anchor relative to the top-left of the original rectangle
            anchor_offset_x = anchor[0] - width // 2
            anchor_offset_y = anchor[1] - height // 2

            # Rotate this anchor offset to account for the rectangle's rotation
            rotated_anchor_x = anchor_offset_x * math.cos(math.radians(rotation)) - anchor_offset_y * math.sin(
                math.radians(rotation))
            rotated_anchor_y = anchor_offset_x * math.sin(math.radians(rotation)) + anchor_offset_y * math.cos(
                math.radians(rotation))

            # Position the rotated rectangle's top-left so the anchor aligns with the given position
            rotated_rect.center = (position[0] - rotated_anchor_x, position[1] - rotated_anchor_y)

            # Blit the rotated surface onto the screen at the calculated position
            self.surface.blit(rotated_surface, rotated_rect.topleft)

    def update(self, seconds, input_manager=None):
        """Update viewport with optional mouse and keyboard input."""
        self.cam.update(seconds)
        
        if input_manager is None:
            return
        
        # Handle keyboard movement and zoom
        if input_manager.direction_x != 0 or input_manager.direction_y != 0:
            self.handle_keyboard_movement(input_manager.direction_x, input_manager.direction_y, seconds, EngineConfig.move_speed)
        if input_manager.zoom_direction != 0:
            self.handle_zoom(input_manager.zoom_direction, seconds, EngineConfig.zoom_speed)
        
        # Handle mouse interactions if provided
        if input_manager.mouse_pos is not None:
            self._handle_mouse_interactions(input_manager.mouse_pos, input_manager.mouse_down, input_manager.mouse_released)
            # Update hover tile if mouse is over this viewport
            if self.is_within(input_manager.mouse_pos[0], input_manager.mouse_pos[1]):
                self.update_hover_tile(input_manager.mouse_pos)
        
        if self.minimap is not None:
            self.minimap.update(seconds)

    def zoom_cam(self, zoom_direction):
        mouse_on_surface = self.mouse_on_surface()
        if self.zoom_level + zoom_direction < 0.1:
            zoom_direction = 0.1 - self.zoom_level
        self.zoom_level += zoom_direction
        self.zoom_level = min(1, max(0.1, self.zoom_level))
        if mouse_on_surface is not None and zoom_direction != 0:
            mouse_in_world = self.surface_to_world(mouse_on_surface)
            self.cam.change(self.cam.center,
                            self.cam.width + self.cam.width * zoom_direction,
                            self.cam.height + self.cam.height * zoom_direction)
            mouse_in_world_after = self.surface_to_world(mouse_on_surface)
            direction_x = mouse_in_world[0] - mouse_in_world_after[0]
            direction_y = mouse_in_world[1] - mouse_in_world_after[1]
            self.cam.change((self.cam.center[0] + direction_x, self.cam.center[1] + direction_y),
                            self.cam.width,
                            self.cam.height)
    def move_cam(self, direction):
        self.cam.change((self.cam.center[0] + direction[0], self.cam.center[1] + direction[1]),
                        self.cam.width,
                        self.cam.height)

    def pan_drag(self, mouse_movement):
        mouse_surface = self.mouse_on_surface()
        if mouse_surface is not None:
            norm_x = mouse_movement[0] / self.screen_width
            norm_y = mouse_movement[1] / self.screen_height
            x_world = self.cam.width * norm_x
            y_world = self.cam.height * norm_y
            self.move_cam((-x_world, -y_world))
            self.moved = True

    def mouse_on_surface(self):
        pos = pygame.mouse.get_pos()
        if self.screen_x1 <= pos[0] <= self.screen_x1 + self.screen_width and self.screen_y1 <= pos[1] <= self.screen_y1 + self.screen_height:
            return pos
        return None

    def is_within(self, x, y):
        return self.screen_x1 <= x <= self.screen_x1 + self.screen_width and self.screen_y1 <= y <= self.screen_y1 + self.screen_height

    def handle_mouse_drag(self, mouse_pos, prev_mouse_pos, mouse_down, mouse_released, is_dragging, dragging_threshold=2):
        """Handle mouse drag panning for this viewport. Returns True if dragging."""
        if not mouse_down:
            return False
        
        if prev_mouse_pos is None:
            return False
        
        if not self.is_primary:
            return False
        
        # Calculate mouse movement
        dx = mouse_pos[0] - prev_mouse_pos[0]
        dy = mouse_pos[1] - prev_mouse_pos[1]
        
        # Start dragging if there's significant movement or already dragging
        if is_dragging or abs(dx) > dragging_threshold or abs(dy) > dragging_threshold:
            self.pan_drag((dx, dy))
            return True
        
        return False

    def handle_mouse_click(self, mouse_pos, mouse_released, was_dragging):
        """Handle mouse click to select tiles. Returns True if a tile was selected."""
        if mouse_released and not was_dragging:
            world_pos = self.surface_to_world(mouse_pos)
            hover_tile = self.map.get_nearest_tile(world_pos)
            if hover_tile is not None:
                self.selected_tile = hover_tile
                return True
        return False

    def update_hover_tile(self, mouse_pos):
        """Update the hover tile based on mouse position."""
        world_pos = self.surface_to_world(mouse_pos)
        self.hover_tile = self.map.get_nearest_tile(world_pos)

    def handle_keyboard_movement(self, direction_x, direction_y, seconds, move_speed):
        """Handle keyboard-based camera movement."""
        if direction_x != 0 or direction_y != 0:
            speed = seconds * self.cam.width * move_speed
            self.move_cam((direction_x * speed, direction_y * speed))

    def handle_zoom(self, zoom_direction, seconds, zoom_speed):
        """Handle zoom input."""
        if zoom_direction != 0:
            self.zoom_cam(zoom_direction * zoom_speed * seconds)

    def _handle_mouse_interactions(self, mouse_pos, mouse_down, mouse_released):
        """Handle mouse interactions (dragging, clicking) for viewport and minimap."""
        surface = self._get_surface_at(mouse_pos[0], mouse_pos[1])
        
        if mouse_down:
            if surface is not None:
                if self.prev_mouse_pos is not None and self.prev_mouse_surface == surface:
                    # Continue dragging on the same surface
                    if surface.is_primary:
                        self.dragging = self.handle_mouse_drag(
                            mouse_pos, self.prev_mouse_pos, mouse_down, mouse_released, self.dragging
                        )
                    elif surface.is_minimap and self.minimap is not None:
                        self.minimap.handle_mouse_drag(mouse_pos, self.prev_mouse_pos, mouse_down)
                elif self.prev_mouse_surface is None and surface.is_minimap and self.minimap is not None:
                    # Start dragging on minimap immediately
                    self.minimap.handle_mouse_drag(mouse_pos, None, mouse_down)
                
                self.prev_mouse_pos = mouse_pos
                self.prev_mouse_surface = surface
        else:
            # Mouse released or not down
            if mouse_released:
                if surface == self:
                    self.handle_mouse_click(mouse_pos, mouse_released, self.dragging)
                self.dragging = False
            self.prev_mouse_pos = None
            self.prev_mouse_surface = None

    def _get_surface_at(self, x, y):
        """Get the surface (viewport or minimap) at the given coordinates."""
        if self.minimap is not None:
            if self.minimap.is_within(x, y):
                return self.minimap
        if self.is_within(x, y):
            return self
        return None
