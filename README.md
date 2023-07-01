# HaremPOV

HaremPOV is a plugin for HaremMate that adds a first person view for all on-screen characters during an H Scene.

## Features

- Change between multiple characters.
- Options to include male and female perspectives.
- Camera adjustments are saved and restored when switching characters.
- Mouse Look Mode allows easier control of the camera with the mouse.
- Camera smoothing option to reduce camera shake from head movement.

## Installation

1. Install [BepInEx]( https://github.com/BepInEx/BepInEx )
1. Install [BepinEx Configuration Manager]( https://github.com/BepInEx/BepInEx.ConfigurationManager )
1. Download and copy the [HaremPOV]( https://github.com/FrostedAshe/HaremPOV/releases/latest ) dll file to the `BepInEx\plugins` folder.

## Usage

During an H Scene, use the following shortcuts:

### Camera Control

| Keys                             | Action                                    |
|----------------------------------|-------------------------------------------|
| F                                | Enable or disable POV mode                |
| Left Mouse Button                | Look around                               |
| Right Mouse Button               | Increase or decrease Field of View        |
| Left Shift + Left Mouse Button   | Roll camera                               |
| Left Control                     | Reset camera rotation and Field of View   |
| V                                | Switch to next character's POV            |
| C                                | Switch to previous character's POV        |
| G                                | Enable or disable camera smoothing        |
| Middle Mouse Button              | Enable or disable Mouse Look Mode         |

### Mouse Look Mode

In Mouse Look Mode, the mouse cursor is hidden and the view direction will change with mouse movement without having to hold down the Left Mouse Button. Mouse Look Mode has additional shortcuts listed below.

| Keys                                     | Action                                    |
|------------------------------------------|-------------------------------------------|
| Middle Mouse Button                      | Disable Mouse Look Mode                   |
| Mouse Movement                           | Look around                               |
| Right Mouse Button                       | Increase or Decrease Field of View        |
| Right Mouse Button + Left Mouse Button   | Roll camera  (Hold RMB first, then LMB)   |
| Left Mouse Button                        | Reset camera rotation and Field of View   |
| Mouse Wheel Scroll Down                  | Switch to next character's POV            |
| Mouse Wheel Scroll Up                    | Switch to previous character's POV        |

## Configuration

Press F1 to open the BepInEx Configuration Manager. Find and open HaremPOV, it has options for:

- Changing the default shortcut keys.
- Changing the default camera Field of View (FOV) when in POV mode.
- Changing the mouse look sensitivity.
- Including or excluding male or female characters' perspectives.
- Changing the amount of camera smoothing (to reduce camera shaking in POV mode).
- Enabling or disabling the use of Mouse Look Mode.

## Building

In order to build the plugin:

- Install the [.NET SDK]( https://dotnet.microsoft.com/en-us/download )
- Clone this repository into a folder.
- Create a folder named `lib` in the project folder.
- Copy the file `Assembly-CSharp.dll` from your HaremMate install folder into the project `lib` folder. ( It is located in `HaremMate\data\Managed` )
- From a shell, change into the project folder and type `dotnet build`. The built dll will be placed in the project `bin` folder.
