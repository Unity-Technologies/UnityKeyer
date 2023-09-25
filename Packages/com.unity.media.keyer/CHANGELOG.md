# Changelog
All notable changes to this package will be documented in this file.

The format is based on [Keep a Changelog](http://keepachangelog.com/en/1.0.0/)
and this project adheres to [Semantic Versioning](http://semver.org/spec/v2.0.0.html).


## [Unreleased]

### Added
- Blue screen support for Color Difference and Despill.
- A Keyer Plot Renderer added to the Keyer Preview Manager Window.
- Display an info label to identify which plot curve is under the mouse cursor.
- A new sample that shows how to use the Blackmagic input video as a source for Keying and displays the final result in the GameView.
- A new sample that shows how to use the Blackmagic input video as a source for Keying and composites the final result with the scene in the GameView.

### Fixed
- An issue with panning in the Preview Window with the left mouse button.
- Improvements to the aligment and layout the Preview Window UI.

### Changed
- Removed Pro License Requirement

## [1.0.0] - 2023-03-15

### Added
- Garbage mask with polygon edition.
- Garbage mask dilation and blending.
- Tooltips in the Keyer inspector UI.
- Display options filtering based on settings.
- Editor analytics.
- Keyer instance list selection in the Manager window.
- Keyer instance scene selection in the Manager window.
- Message when no Keyer is selected in the Preview Window.
- Info message about usage of Alt/Option modifier in the Preview Window.
- A new sample targeting URP that shows how to use the Blackmagic input video as a source for Keying and Compositing.
- New samples for Keying using Video Clips. 

### Fixed
- Blur UV sampling coordinates.
- An issue where an exception was thrown while changing the Keyer Settings.
- Removed an incorrect option switch from the core mask UI.
- A rendering issue of the despill when image height is greater than image width.
- Errors in the console when the Preview Window is not visible but active.
- Issues within the Keyer Editor of the Preview Window as settings or textures were edited.
- Preview fixes regarding transform management.
- Refactored commandBuffer execution.
- Polygon Editor aspect ratio handling.
- Signed Distance Field Shader warning on Metal.
- Polygon Editor point deletion on Mac keyboards.
- Soft Mask activation issue.

### Changed
- Updated the required minimum Unity Editor version to Unity 2022.2.
- Renamed the game objects created by the one-click option to create a default Keyer.
- Renamed the button to open the Preview Window.
- Changed the images used in the samples.

## [1.0.0-exp.7] - 2023-01-17

### Added 
- A new Keyer Preview Manager window.
- A new sample that shows how to use the Blackmagic input video as a source for Keying and Compositing.

### Fixed
- Fix missing references in keyer samples files.
- Fix unnecessary graph rebuilds.

## [1.0.0-exp.6] - 2022-10-26

### Added
- A new one-click option to create a default Keyer.
- Added options to enable/disable optional parts of the Keyer.
- A new algorithm to provide an alternative and advanced way to key images.
- A public API for the Keyer.
- Added three samples of the Multipass Keyer for the Built-in, URP, and HDRP render pipelines.
- Added an erode core mask node.
- Added Color Distance segmentation algorithm.
- Added support to update the keyer render graph when keyer properties are modified through scripting.

### Changed
- Updated the required minimum Unity Editor version for the Keyer package to Unity 2022.1.
- Improved the UI for the Keyer inspector by using UI Toolkit.
- Simplified Color Difference parameters.
- Cleanup of the Blackmagic_HDRP_Compositing project.

### Fixed
- An issue with creating the Keyer in the built-in render pipeline.
- Refactored rendering for better performance and GPU memory usage.
- An issue when the Keyer settings are set to none.

## [1.0.0-exp.5] - 2022-07-08
### Added
- The Keyer component persists data saved as Keyer Settings as assets in the project. 

## [1.0.0-exp.4] - 2022-05-16
### Added
- A blur node that allows for softening of the core mask.
- A clip mask node that allows for eroding of the core mask. This permits the edge to be controlled by using *only* the soft mask.
- A blendmax node to merge the core opaque mask with a soft mask for the edge.
- Two Color Difference nodes in the pipeline to allow two sets of parameters.
- A license check to ensure Unity Pro is installed on the current machine.

## [1.0.0-exp.3] - 2022-05-05
### Added
- Creation of a pipeline of image processing nodes to improve the resulting key.

## [1.0.0-exp.1] - 2022-01-18
### Added
- Initial package structure.

### This is the first release of *Unity Package com.unity.media.keyer*.

This version includes a color difference Keyer integrated in the
sample project SampleProjects/Blackmagic_HDRP_Compositing.
