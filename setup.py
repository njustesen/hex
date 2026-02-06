from setuptools import setup, find_packages

setup(
    name="hex",
    version="0.1.0",
    description="Hex game engine",
    packages=find_packages(),
    python_requires=">=3.6",
    install_requires=[
        "pygame",
        "matplotlib",
    ],
)
