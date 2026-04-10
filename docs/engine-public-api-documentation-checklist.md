# Cyberland.Engine public API documentation checklist

Check each row when the type and all its **public** members have XML documentation (`///`) per the plan: novice/modder audience, `cref` links, units, threading where relevant.

## Core / ECS

- [x] `ComponentId`
- [x] `ComponentStore<T>`
- [x] `IComponentStore`
- [x] `EntityId`
- [x] `EntityRegistry`
- [x] `World`
- [x] `ISystem`
- [x] `IParallelSystem`
- [x] `DelegateSequentialSystem`
- [x] `ChunkQuery<T>`
- [x] `ComponentChunkView<T>`
- [x] `ChunkQueryEnumerator<T>`
- [x] `ChunkQuery2<T0,T1>`
- [x] `ChunkQueryEnumerator2<T0,T1>`
- [x] `ComponentChunkView2<T0,T1>`
- [x] `SimdFloat`

## Core / Tasks

- [x] `SystemScheduler`
- [x] `ParallelismSettings`

## Hosting

- [x] `GameHostServices`

## Modding

- [x] `IMod`
- [x] `ModLoadContext`
- [x] `ModManifest`
- [x] `ModLoader`
- [x] `ExcludeModsParser`

## Root

- [x] `GameApplication`
- [x] `WorldScreenSpace`

## Scene — components

- [x] `Position`
- [x] `Rotation`
- [x] `Scale`
- [x] `Transform`
- [x] `TransformMath`
- [x] `Sprite`
- [x] `SpriteAnimation`
- [x] `Tilemap`
- [x] `ParticleEmitter`

## Scene — stores & interfaces

- [x] `ITilemapDataStore`
- [x] `TilemapDataStore`
- [x] `ParticleStore`

## Scene — systems

- [x] `SpriteRenderSystem`
- [x] `TilemapRenderSystem`
- [x] `SpriteAnimationSystem`
- [x] `SpriteAnimationMath`
- [x] `TransformHierarchySystem`
- [x] `ParticleSimulationSystem`
- [x] `ParticleRenderSystem`

## Rendering — types

- [x] `IRenderer`
- [x] `SpriteLayer` (enum)
- [x] `SpriteDrawRequest`
- [x] `PointLight`
- [x] `SpotLight`
- [x] `DirectionalLight`
- [x] `AmbientLight`
- [x] `PostProcessVolume`
- [x] `PostProcessOverrides`
- [x] `GlobalPostProcessSettings`

## Rendering — implementation & helpers

- [x] `VulkanRenderer` (all partial files; one narrative)
- [x] `GlslSpirvCompiler`
- [x] `SpriteDrawSorter`
- [x] `PostProcessVolumeMerge`
- [x] `EngineDefaultGlobalPostProcess`
- [x] `GraphicsInitializationException`
- [x] `UserMessageDialog`

## Assets

- [x] `AssetManager`
- [x] `VirtualFileSystem`

## Input

- [x] `KeyBindingStore`
- [x] `InputAction`

## Audio

- [x] `OpenALAudioDevice`

## Localization

- [x] `LocalizationManager`
- [x] `LocalizationBootstrap`

## Optional (internal — maintainers)

- [x] Internal `Core/Ecs` types (`ArchetypeWorld`, `Archetype`, `ArchetypeChunk`, `Column`, etc.)
