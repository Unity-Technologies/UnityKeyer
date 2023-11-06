# Chroma Keying with the Unity Editor

Use the Unity Keyer package (`com.unity.media.keyer`) to create an alpha mask from green screen images.

This repository contains the code package, the test package and test projects related with Unity Keyer.

## Get started

To learn about the Unity Keyer package (concepts, features, and workflows) read the [Keyer package documentation](Packages/com.unity.media.keyer/Documentation~/index.md) in this repository.
For user convenience, an HTLM build is also available [here](Packages/com.unity.media.keyer/Built-documentation.zip).

### Check out the licensing model

The Keyer package is licensed under the [under the Apache License, Version 2.0](LICENSE.md).

### Contribution and maintenance

We appreciate your interest in contributing to the Unity Keyer package.   
It's important to note that **this package is provided as is, without any maintenance or release plan.**   
Therefore, we are unable to monitor bug reports, accept feature requests, or review pull requests for this package.

However, we understand that users may want to make improvements to the package.    
In that case, we recommend that you fork the repository. This will allow you to make changes and enhancements as you see fit.

## Keyer package

### Access the Keyer package folder

| Package                                     | Description                                                                   |
|:--------------------------------------------|:------------------------------------------------------------------------------|
| **[Keyer](Packages/com.unity.media.keyer)** | The package that allows you to create an alpha mask from green screen images. |

### Test the Keyer package

Use these Unity projects to run various tests against the Keyer package.

| Project                | Description                                                       |
|------------------------|-------------------------------------------------------------------|
| Keyer-api-tests        | Api tests                                                         |
| KeyerGraphicsTestsHDRP | Runs the pipelines tests with HDRP installed.                     |
| KeyerGraphicsTestsLRP  | Runs the pipelines tests on the Legacy (built-in) render pipeline |
| KeyerGraphicsTestsURP  | Runs the pipelines tests with URP installed.                      |

These test projects use an additional internal package available in this repository:

|  Package                                                                  | Description                                                                   |
|:--------------------------------------------------------------------------|:------------------------------------------------------------------------------|
| **[Keyer Graphics Tests](Packages/com.unity.media.keyer.graphics-tests)** | The package that allows you to create an alpha mask from green screen images. |
