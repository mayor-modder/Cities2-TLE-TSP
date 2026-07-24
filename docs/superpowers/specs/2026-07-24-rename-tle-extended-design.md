# Traffic Light Enhancements Extended display-name design

## Goal

Present the local mod and release package as `Traffic Light Enhancements Extended` everywhere players identify the product.

## Compatibility boundary

The assembly names, namespaces, localization keys, UI binding namespace, serialized component types, and mod identifier remain `C2VM.TrafficLightsEnhancement`. These identifiers are compatibility contracts and are not part of the player-facing rename.

## Design

The local deployment folder and release archive folder become `TrafficLightEnhancementsExtended`, because mod folders cannot contain spaces and Skyve presents the CamelCase folder name as `Traffic Light Enhancements Extended`. The UI build receives a separate deploy-folder value so its internal module ID remains unchanged. The settings section, key-binding map, publisher display name, and English product-name references use the spaced display name.

The old `C2VM.TrafficLightsEnhancement` local folder is removed only after Cities: Skylines II is confirmed closed and the renamed build is installed successfully. Skyve is then restarted normally to force a rescan.

## Verification

Automated checks protect the exact display name, internal identifiers, local UI output folder, publisher name, and release archive folder. Full C#, serialization, and UI suites run before installation. The installed DLL hashes must match the build, the old folder must be absent, and Skyve must classify the renamed folder as the sole TLE Extended mod.
