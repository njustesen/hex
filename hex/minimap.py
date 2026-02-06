import pygame
from hex.viewport import Viewport


class Minimap(Viewport):

    def __init__(self,
                 screen_x1,
                 screen_y1,
                 screen_width,
                 screen_height,
                 zoom_level,
                 map,
                 primary_viewport=None,
                 color=None):
        # Initialize with minimap-specific defaults
        super().__init__(
            screen_x1=screen_x1,
            screen_y1=screen_y1,
            screen_width=screen_width,
            screen_height=screen_height,
            zoom_level=zoom_level,
            map=map,
            minimap=None,  # Minimap doesn't have its own minimap
            can_zoom=False,  # Minimap typically doesn't zoom
            can_move=False,  # Minimap typically doesn't move
            color=color if color is not None else (0, 0, 0),
            primary_viewport=primary_viewport,
            is_minimap=True,
            is_primary=False
        )
        self.primary_viewport = primary_viewport

    def draw(self, grid=None):
        self.surface.fill(self.color)
        if grid is not None:
            self._draw_grid(grid)
        self.draw_map()
        self.draw_camera()

    def draw_camera(self):
        """Draw a rectangle showing the camera view of the primary viewport."""
        if self.primary_viewport is None:
            return
        x1, x2 = self.primary_viewport.cam.x1, self.primary_viewport.cam.x2
        y1, y2 = self.primary_viewport.cam.y1, self.primary_viewport.cam.y2
        x1, y1 = self.world_to_surface((x1, y1))
        x2, y2 = self.world_to_surface((x2, y2))
        x1 = max(0, x1)
        y1 = max(0, y1)
        x2 = min(self.screen_width, x2)
        y2 = min(self.screen_height, y2)
        width = x2 - x1
        height = y2 - y1
        pygame.draw.rect(self.surface, (255, 255, 255), (x1, y1, width, height), 1)

    def handle_mouse_drag(self, mouse_pos, prev_mouse_pos, mouse_down):
        """Handle mouse drag panning on the minimap to move the primary viewport."""
        if not mouse_down:
            return False
        
        pos = self.mouse_on_surface()
        if pos is None:
            return False
        
        surface_x1, surface_y1 = pos
        world_x1, world_y1 = self.surface_to_world((surface_x1, surface_y1))
        
        if self.primary_viewport is not None:
            self.primary_viewport.cam.change(center=(world_x1, world_y1), map=self.primary_viewport.map)
            self.primary_viewport.moved = True
            return True
        
        return False
