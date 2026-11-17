# Olve.Engine3D

A 3D engine in C# on WebGPU, written from scratch. The same code runs on the desktop (Dawn) and in the
browser (the browser's own WebGPU), with windowing and input from [Silk.NET](https://github.com/dotnet/Silk.NET).
It is the engine behind *On Track To Grow*, a train-network builder.

**Try it:** [engine.ovea.pro](https://engine.ovea.pro) runs a sample of this engine in your browser (it needs WebGPU).

## What's in it

- **Rendering:** one instanced pipeline for all 2D and 3D drawing, with sorted render groups and
  per-group blend and depth state. Geometry and instance ids are typed (`GeometryId<TVertex>`,
  `GroupId<TInstance, TInputs>`), so mixing vertex or instance types is a compile error.
- **Techniques:** Hermite-spline meshes, MSDF text, GUI rectangles with borders and rounded corners
  drawn in the shader, and heightmap terrain with a grid overlay.
- **Scenes, GUI, camera, input, events and physics**, with a command interface that can query the full
  engine state as JSON.
- **Audio:** a patch synthesizer (oscillators, noise, filters, envelopes, distortion) that renders
  sounds block by block, a mixer with buses, and backends for OpenAL on the desktop and Web Audio in
  the browser.

## Layout

```
src/Olve.Engine3D/                    the engine
src/Olve.Engine3D.Graphics.WebGPU/    WebGPU backend, through Olve.WebGPU
src/Olve.Engine3D.Audio.OpenAL/       desktop audio
src/Olve.Engine3D.Platform.Desktop/   desktop window, input and frame loop
src/Olve.Engine3D.Platform.Web/       browser host
samples/                              a triangle, on the desktop and in the browser
tests/Olve.Engine3D.Tests/            unit tests (TUnit)
```

The samples' shaders are written in Slang (`resources/shaders/`). The C# and WGSL generated from them
are included under `assets/`, because the tool that generates them isn't published yet.

## Building

You need the .NET 11 SDK. The browser projects also need `dotnet workload install wasm-tools`.

```bash
dotnet build Olve.Engine3D.slnx
dotnet run --project samples/Olve.Engine3D.Samples.Triangle.Desktop
dotnet test --project tests/Olve.Engine3D.Tests
```

## How this repo is made

The engine is developed together with the game in a private repository. Every build of the game
publishes the engine's source to `main` as one commit, on top of the files on the
[`static`](../../tree/static) branch: this README, the license and the pipeline in `.pipelines/`, which
builds the browser sample and deploys it to the demo above. So `main` has no history of its own and the
repo doesn't take pull requests. The publishing and the demo run on
[Olve.Pipelines](https://github.com/OliverVea/Olve.Pipelines), the CI/CD service I wrote.

## License

[MIT](LICENSE)
