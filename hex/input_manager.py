import pygame


class InputManager:

    def __init__(self):
        self.keys_down = set()
        self.keys_pressed = set()
        self.keys_released = set()
        self.mouse_down = False
        self.mouse_released = False
        self.mouse_pos = (0, 0)
        self.zoom_direction = 0
        self.direction_x = 0
        self.direction_y = 0

    def update(self, events, mouse):
        """Update input state from events and mouse position."""
        # Clear frame-specific state
        self.keys_pressed.clear()
        self.keys_released.clear()
        self.zoom_direction = 0
        
        # Update mouse position
        self.mouse_pos = mouse
        
        # Process events
        for event in events:
            if event.type == pygame.KEYDOWN:
                if event.key not in self.keys_down:
                    self.keys_pressed.add(event.key)
                self.keys_down.add(event.key)
            elif event.type == pygame.KEYUP:
                self.keys_released.add(event.key)
                if event.key in self.keys_down:
                    self.keys_down.remove(event.key)
            elif event.type == pygame.MOUSEWHEEL:
                self.zoom_direction = event.y
        
        # Update mouse button state
        mouse_down = pygame.mouse.get_pressed()[0]
        self.mouse_released = self.mouse_down and not mouse_down
        self.mouse_down = mouse_down
        
        # Calculate movement direction from keys
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
