# Micromobility Intersection Simulator

A Unity simulation of a Los Angeles style city where a bicycle or
e-scooter rider navigates intersections with traffic signals and
traffic. The simulator measures safety using time to collision and
logs each run.

## Features
- Large city with buildings, roads, crosswalks, and green bike lanes
- Traffic signals with red, yellow, and green phases and a red light warning
- Three selectable scenarios, including a right hook crash with a fast overtaking car
- Live display of speed, time to collision, minimum time to collision, and near miss detection
- Counter for red lights run
- Each run is saved to a CSV log file for analysis
- Game over when the rider leaves the road

## Controls
- W / S: speed up and slow down
- A / D: steer
- C: change camera
- 1 / 2 / 3: choose scenario
- R: restart

## Demo
[Watch the demo video](micromobility_simulator_demo.mp4)

## Built With
Unity 6.3 LTS, C#

## Project Contents
This repository contains the scripts, scenes, and project settings.
Large assets are not included because of file size limits. The demo
video shows the full simulation.
