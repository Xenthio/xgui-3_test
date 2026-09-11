# TigerThemeSampler

Renders standard Mac OS X 10.4 Aqua push-button states through Carbon HITheme and writes transparent TGA reference sheets plus TSV metadata.

## Build on Tiger

From the sampler directory on the Tiger VM:

```sh
gcc -o TigerThemeSampler TigerThemeSampler.c \
  -isysroot /Developer/SDKs/MacOSX10.4u.sdk \
  -framework Carbon -framework ApplicationServices
```

Run it with an output location:

```sh
./TigerThemeSampler /tmp/TigerThemeSampler.tga /tmp/TigerThemeSampler.tsv 1 /tmp/TigerThemeSampler-states
```

The sampler renders 21px-high push buttons. The TGA contains transparent rows for normal, default, pressed, focused, inactive, disabled, and disabled-inactive buttons. The third argument controls supersampling; use `1`, `2`, or `4` to produce native, 2x, or 4x references. The optional fourth argument writes individually named state TGAs (`normal.tga`, `default.tga`, and so on). The TSV contains top-left row and button bounds to use when sampling pixels.

The repository artifact `Artifacts/TigerThemeSampler-1x-states/` contains the individually labelled 176x21 state exports. `Artifacts/TigerThemeSampler-1x.tga` is the combined 240x336 sheet, with its matching coordinates in `Artifacts/TigerThemeSampler-1x.tsv`.

HITheme provides the final themed render, not its private gradient stops. The sampler therefore preserves the exact Tiger output while making each control state easy to inspect and sample.
