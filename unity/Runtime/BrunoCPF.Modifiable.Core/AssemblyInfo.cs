using System.Runtime.CompilerServices;

// ModifiableProperty (the reactive BrunoCPF.Modifiable assembly) evaluates through the same
// internal helpers as ModifierStack and DeltaPipeline, and its records use this assembly's
// IsExternalInit polyfill rather than declaring a second copy.
[assembly: InternalsVisibleTo("BrunoCPF.Modifiable")]
