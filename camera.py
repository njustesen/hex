import random
import pygame


class Camera:

    def __init__(self, center, width, height):
        self._center = center
        self.width = width
        self.height = height
        self.shake_offset_x = 0
        self.shake_offset_y = 0
        self.shaking_time = 0

    def rel_mouse(self):
        pos = pygame.mouse.get_pos()
        if self.x1 <= pos[0] <= self.x1 + self.width and self.y1 <= pos[1] <= self.y1 + self.height:
            return pos
        return None

    @property
    def rect(self):
        return pygame.Rect(self.x1, self.y1, self.width, self.height)

    @property
    def _rect(self):
        return pygame.Rect(self._x1, self._y1, self.width, self.height)

    def is_within(self, pos, size=0):
        return self.is_within_xy(pos[0], pos[1], size=size)

    def is_within_xy(self, x, y, size=0):
        return self.x1 <= x + size and x - size <= self.x2 and self.y1 <= y + size and y - size <= self.y2

    @property
    def x1(self):
        return self.center[0] - self.width / 2

    @property
    def x2(self):
        return self.center[0] + self.width / 2

    @property
    def y1(self):
        return self.center[1] - self.height / 2

    @property
    def y2(self):
        return self.center[1] + self.height / 2

    @property
    def _x1(self):
        return self._center[0] - self.width / 2

    @property
    def _x2(self):
        return self._center[0] + self.width / 2

    @property
    def _y1(self):
        return self._center[1] - self.height / 2

    @property
    def _y2(self):
        return self._center[1] + self.height / 2

    @property
    def center(self):
        return self._center[0] + self.shake_offset_x, self._center[1] + self.shake_offset_y

    def set_center(self, position, bounds=None):
        _center = position
        if bounds:
            self._adjust(bounds)

    def norm(self, pos):
        return (pos[0] - self.x1) / self.width, (pos[1] - self.y1) / self.height

    def change(self, center, width, height, bounds=None):
        self._center = center
        self.width = width
        self.height = height
        if bounds:
            self._adjust(bounds)

    def _adjust(self, bounds):
        return
        if self.width > bounds.width:
            self._center = (bounds.x + bounds.width / 2, self._center[1])
        if self.height > bounds.height:
            self._center = (self._center[0], bounds.y + bounds.height / 2)
        width = self.width
        height = self.height
        if self._x1 < bounds.x:
            self._center = (bounds.x + width / 2, self._center[1])
        if self._x2 > bounds.x + width:
            self._center = ((bounds.x + width) - width / 2, self._center[1])
        if self._y1 < bounds.y:
            self._center = (self._center[0], bounds.y + height / 2)
        if self._y2 > bounds.y + height:
            self._center = (self._center[0], (bounds.y + height) - height / 2)

    def screen_shake(self, seconds):
        self.shaking_time = seconds

    def shake(self, amount):
        x = random.randint(-1, 1)
        y = random.randint(-1, 1)
        self.shake_offset_x = x * amount * self.width
        self.shake_offset_y = y * amount * self.height

    def update(self, seconds, shake=0.05):
        if self.shaking_time > 0:
            self.shake(self.shaking_time * shake)
        self.shaking_time *= 0.95
        if self.shaking_time < 0:
            self.shaking_time = 0
