# NEngine

## Summary
A Game Engine/Editor written in C# (because I like it) built on SFML (because it's simple enough) for my own learning purposes.

This project is a follow-up to earlier design attempts with modularity in mind as well as a well defined set
of Engine Intrinsics and Core Libraries.

The engine itself is meant to commit to a common API that may be reimplemented to use a different rendering
strategy such as OpenGL when I move on from SFML.

## Motivation
Mainly to improve my skills as a software architect and programmer.

I've always loved games programming and have had a desire to dig into what makes it all work.

## How to Build
### Requirements
[dotnet 8.0](https://dotnet.microsoft.com/en-us/download) or greater

### To Build
Run the build script provided in the root directory.
It should invoke dotnet build in release mode and runs the release binary of NEngineEditor.

You may also use the built engine binaries without the editor as the editor itself is merely a visual
scene editor which provides projects with a base entrypoint which evaluates the generated scene files.