# Third-party notices and references

MySharp source is licensed under Apache-2.0; dependencies retain their own licenses.

| Dependency | Use | License / source |
|---|---|---|
| Avalonia 12.1.4 and desktop/theme/font packages | Native controls, layout, input, styling and desktop hosting | [MIT](https://github.com/AvaloniaUI/Avalonia/blob/master/licence.md) |
| Inter | Typeface supplied through Avalonia.Fonts.Inter | [SIL Open Font License 1.1](https://github.com/rsms/inter/blob/master/LICENSE.txt) |
| Lucide.Avalonia 0.2.26 | Preconverted vector icon controls and geometry | [MIT](licenses/lucide-avalonia.txt) |
| Lucide / Feather icons | Icon artwork | [ISC and MIT notices](licenses/lucide.txt) |
| .NET / Roslyn SDK components | Runtime/compiler; the standalone formatter references the installed SDK's Roslyn assemblies | [MIT](https://github.com/dotnet/roslyn/blob/main/License.txt) |

Transitive/runtime components (including SkiaSharp and HarfBuzzSharp) have their own notices supplied by their upstream distributions. Retain those notices when distributing applications containing them.

Design references: [MyGo](https://github.com/egoist/mygo) and Plico inspired the context/children API, retained-node identity and compact formatting approach. This C# implementation uses Avalonia's controls/property system, not a copy of their renderer. The design notes identify which mechanisms were studied.
