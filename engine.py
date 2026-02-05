import pygame
import colors
import sys
import platform
import subprocess
import re
from map import HexGridMap, TileGridMap, IsometricTileGridMap
from viewport import Viewport
from minimap import Minimap
from unit import Unit
from input_manager import InputManager
from engine_config import EngineConfig


class GameEngine:

    def __init__(self):
        # Get the actual screen resolution
        screen_width, screen_height = self._get_screen_resolution()
        print(f"Screen width: {screen_width}, screen height: {screen_height}")
        
        if EngineConfig.fullscreen:
            EngineConfig.width = screen_width
            EngineConfig.height = screen_height
            self.screen = pygame.display.set_mode((EngineConfig.width, EngineConfig.height), pygame.FULLSCREEN)
        else:
            EngineConfig.width = screen_width
            EngineConfig.height = screen_height
            EngineConfig.fullscreen = False
            self.screen = pygame.display.set_mode((EngineConfig.width, EngineConfig.height))
        self.done = False
        pygame.mouse.set_visible(False)
        self.map = HexGridMap(21, 11, hex_radius=100, hex_vertical_scale=0.9)
        self.map.tiles[10][10].unit = Unit()
        #self.map = TileGridMap(30, 30, tile_width=80, tile_height=120)
        #self.map = IsometricTileGridMap(20, 20, tile_width=60, tile_height=40)

        minimap_width = EngineConfig.width / 5
        minimap_height = EngineConfig.height / 5

        self.viewport = Viewport(screen_x1=0,
                                 screen_y1=0,
                                 screen_width=EngineConfig.width,
                                 screen_height=EngineConfig.height,
                                 zoom_level=1,
                                 map=self.map,
                                 minimap=None,
                                 can_zoom=False,
                                 can_move=False,
                                 is_minimap=False,
                                 is_primary=True)

        self.minimap = Minimap(screen_x1=EngineConfig.width - minimap_width - 1,
                               screen_y1=EngineConfig.height - minimap_height - 1,
                               screen_width=minimap_width,
                               screen_height=minimap_height,
                               zoom_level=1,
                               map=self.map,
                               primary_viewport=self.viewport)

        self.viewport.minimap = self.minimap

        self.input_manager = InputManager()

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
        self._check_quit(events)
        self.input_manager.update(events, mouse)
        self.viewport.update(seconds, input_manager=self.input_manager)

    def draw(self, events, mouse):
        self.screen.fill(colors.BLACK)
        self.draw_viewport(self.viewport)
        if self.minimap is not None:
            self.draw_viewport(self.minimap, border=1)
        pygame.draw.rect(self.screen, (255, 255, 255), [mouse[0]-5, mouse[1]-5, 10, 10])
        pygame.display.flip()

    def draw_viewport(self, viewport: Viewport, border=0, border_color=colors.RED):
        viewport.draw()
        self.screen.blit(viewport.surface, (viewport.screen_x1, viewport.screen_y1))
        if border > 0:
            pygame.draw.rect(self.screen, border_color, [viewport.screen_x1-border, viewport.screen_y1-border, viewport.screen_width+2*border, viewport.screen_height+2*border], border)
    
    def _get_screen_resolution(self):
        """Get the actual physical screen resolution, especially important on macOS Retina displays."""
        if platform.system() == 'Darwin':  # macOS
            try:
                # Use system_profiler to get actual display resolution on macOS
                result = subprocess.run(
                    ['system_profiler', 'SPDisplaysDataType'],
                    capture_output=True,
                    text=True,
                    timeout=5
                )
                if result.returncode == 0:
                    # Parse the output to find resolution
                    lines = result.stdout.split('\n')
                    for i, line in enumerate(lines):
                        if 'Resolution:' in line:
                            # Look for resolution like "2880 x 1800" or "2560 x 1600"
                            parts = line.split('Resolution:')
                            if len(parts) > 1:
                                res_text = parts[1].strip()
                                # Extract numbers
                                numbers = re.findall(r'\d+', res_text)
                                if len(numbers) >= 2:
                                    return int(numbers[0]), int(numbers[1])
            except Exception as e:
                print(f"Warning: Could not get macOS screen resolution: {e}")
        
        # Fallback: use pygame's get_desktop_sizes() or display info
        try:
            desktop_sizes = pygame.display.get_desktop_sizes()
            if desktop_sizes:
                return desktop_sizes[0]
        except:
            pass
        
        # Final fallback
        display_info = pygame.display.Info()
        return display_info.current_w, display_info.current_h
    
    def _check_quit(self, events):
        for event in events:
            if event.type == pygame.QUIT:
                pygame.quit()
                sys.exit()
