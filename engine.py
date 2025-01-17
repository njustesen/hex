import pygame
import colors
import sys
from map import Map


class GameEngine:

    def __init__(self, width, height, fullscreen=False):
        self.width = width
        self.height = height
        self.fullscreen = fullscreen
        self.screen = pygame.display.set_mode((width, height))
        if fullscreen:
            pygame.display.toggle_fullscreen()
        self.done = False
        pygame.mouse.set_visible(False)
        size = 50
        self.map = Map(10, 10, hex_radius=100, hex_vertical_scale=1)
        self.hover_hex = None

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
        for event in events:
            if event.type == pygame.QUIT:
                pygame.quit()
                sys.exit()
        self.hover_hex = self.map.get_nearest_hex(mouse)

    def draw(self, events, mouse):
        self.screen.fill(colors.BLACK)
        self.map.draw(self.screen, hover=self.hover_hex)
        pygame.draw.rect(self.screen, (255, 255, 255), [mouse[0]-5, mouse[1]-5, 10, 10])
        pygame.display.flip()
