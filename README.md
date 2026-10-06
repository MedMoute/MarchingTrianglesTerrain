## Herrmyth's C# Terrain Authoring Toolkit 

A full C# rewrite of [Yūgen's Terrain Authoring Toolkit](https://github.com/ToumaKamijou/Yugens-Terrain-Authoring-Toolkit) with a twist !

This project's goal is to provide a powerful terrain editor in Godot that can provide the three following key features:

 * An in-editor tool that will allow for a simple yet robust terrain mesh generation.
 * An implicit tiling of the plane, consistent with the generated terrain.
 * A stable API to leverage the properties of the terrain within the game code.

### What do you mean by "implicit tiling of the plane" ?

I mean hexagons. Because Bees.
<p align=center>
<img width="320" height="180" alt="Hexagons_are_the_Bestagons" src="https://github.com/user-attachments/assets/2ce12f6b-e757-4297-a500-3d31ff9205e4" /></p>

Jokes aside, the key of the project is to be using a Hexagonal Grid as the implicit tiling of the plane, and a surface reconstruction algorithm based on the Marching Triangles Algorithm to generate a terrain surface.

### Installation

If you have Godot and use the C# SDK you can clone the `master` branch and use the add-on as-is.
For .gd users, wait for a release to get a packaged version.

### How to use

The UI is based on [Yūgen's Terrain Authoring Toolkit](https://github.com/ToumaKamijou/Yugens-Terrain-Authoring-Toolkit) so if you're familiar with that plugin you should  not be lost.
Otherwise please visit the Get Started page on the wiki (TODO)

### Planned Features :

The 0.1 Roadmap is [here](https://github.com/MedMoute/MarchingTrianglesTerrain/issues?q=milestone%3A0.1)
The goal is to provide a serviceable MVP for all of the 3 key features of the project

#### Known Issues :

* The geometry behaviour is not propagated accross chunk borders [#29](https://github.com/MedMoute/MarchingTrianglesTerrain/issues/29)


### Credit

Development by [HerrMyth](https://github.com/MedMoute)
This project initially stemmed from my interest into [Yūgen's Terrain Authoring Toolkit](https://github.com/ToumaKamijou/Yugens-Terrain-Authoring-Toolkit), written by [Yugen](https://www.youtube.com/@yugen_seishin) as well as [a larger team](https://github.com/ToumaKamijou/Yugens-Terrain-Authoring-Toolkit#credits) since 1.1.0 .

