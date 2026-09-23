## 001 - Introducing Cell Triangulation and Triangulation Edit Actions

#### Context:

In order to allow the user to freely define the behavior of the terrain cells, the need for an intermediate
abstraction of the modified triangle shaped that will form the terrain mesh once added to a surface appeared
quickly when the earlier implementations tried to bruteforce the geometry creation. It quickly led to a mess
where some triangles that were created to support some cases broke other cases.

In the context of terrain mesh creation, the "atoms" are the `HexTerrainCell`s that are processed independently one from another.
Since the hexagonal cells are explicitly split in 6 "equilateral" triangles, we actually can process the geometry of each triangle
separately.

#### Requirements : 

Support local modification of a triangle's geometry to meet functional requirement, while making sure of the two following points:
 1. The projection of the modified geometry on the xOz plane must be strictly identical to the one of the initial triangle.
 2. The modified geometry must be an orientable connected bounded manifold of order 2 with its Euler characteristic equal to 1 
 (e.g. a space homeomorphic to a disk)

The locally-defined requirement is either set per-chunk or manually edited on a hexagonal-cell edge basis.
The requirement will be user defined but since we want the output of the modifications to be usable as a mesh, we have the following constraint
 3. The sum of the local applications of the modification must also be a space homeomorphic to a disk
   (e.g. the modifications of a list of triangles will also be a connected bounded surface provided that the triangles were initially neighbors)

#### Implemented solution : `Triangulation` and `TriangulationEditAction`

We introduce the concept of `Triangulation` as a space homeomorphic to the surface defined as a Triangle that follows the 1. and 2. requirements.

Implementation-wise, a `Triangulation` is defined by its vertices, edges and its boundary. 

We also introduce the concept of `TriangulationEditAction` which is an application that:
 * Follows the 3. requirement
 * Transforms any `Triangulation` into a `Triangulation`

Therefore, applying a list of `TriangulationEditAction` to the `Triangulation` implied by a single initial triangle will result
in a `Triangulation` that also follows 1. and 2., while applying each operation locally on several triangulations will lead to 
several triangulations following the 3. requirement.

We do not make any proof that it's true, but that's the gist of the implementation.

By defining the modifications of the `Triangulations` as a List of curated `TriangulationEditAction`, we can safely apply any modification,
provided it can be described as such.

We implement the following `TriangulationEditAction`:
  * `MovePointAlongYAxis` : Action that Displaces a triangulation's vertex along the Y axis to a provided height.
  * `DisplaceEdgeAlongYAxis` : Action that Displaces a triangulation's edge along the Y axis.
  * `SplitSubEdge` : Action that splits a subEdge of the triangulation at a given point defined by a weight between the sub-edge's starting and ending vertices.
  * `AddTriangleOnBorderEdge` : Action that appends a triangle on an edge of the triangulation.
  * `AddTriangFan` : Defines a triangle from a pair of vertex indexes and a third point. 

