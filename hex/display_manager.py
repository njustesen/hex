import pygame
import platform
import subprocess
import re


class DisplayManager:
    """Manages display-related operations, especially screen resolution detection."""
    
    @staticmethod
    def get_screen_resolution():
        """Get the actual physical screen resolution, especially important on macOS Retina displays.
        
        Returns:
            tuple: (width, height) of the screen resolution
        """
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
                pass
        
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
