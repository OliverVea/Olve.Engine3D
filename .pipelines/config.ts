import { config, helmTarget, z } from "@olivervea/pipelines";

export const DEMO = config("demo", "Where the browser demo's image is pushed and where it's deployed.", z.object({
  repository: z.string().describe("Registry repository for the demo's image, e.g. registry.example.com/engine-demo."),
  registryMap: z.record(z.string(), z.string()).optional().describe("Pull-through caches for the base images (registry → mirror prefix); unset = none."),
  target: helmTarget({ release: "engine-demo" }),
}));
