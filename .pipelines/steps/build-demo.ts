import { implement, step, Site, Source } from "@olivervea/pipelines";
import buildDemo from "../scripts/build-demo.sh" with { type: "file" };

const WORK = "/work/src";
const OUT = "/work/out";

export const buildDemoStep = implement(step({
  name: "build-demo",
  description: "Publishes the browser sample (wasm) as the demo's static files.",
  image: "mcr.microsoft.com/dotnet/sdk:11.0",
  input: { source: Source },
  output: Site,
  timeoutSeconds: 1800,
}), async ({ source }, ctx) => {
  await ctx.sh(buildDemo, {
    env: {
      TREE: await ctx.files.fetch(source.tree),
      WORK,
      OUT,
      // Leftover MSBuild nodes and the compiler server would hold the step's output pipe open after dotnet exits.
      MSBUILDDISABLENODEREUSE: "1",
      UseSharedCompilation: "false",
    },
  });
  return { root: await ctx.output.dir(`${OUT}/wwwroot`, { name: "demo-site" }) };
});
