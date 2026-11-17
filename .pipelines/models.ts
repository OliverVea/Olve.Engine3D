import { model, z } from "@olivervea/pipelines";

export const DeployRequest = model("DeployRequest", z.object({
  repo: z.string().default("OliverVea/Olve.Engine3D"),
  sha: z.string().regex(/^[0-9a-f]{40}$/),
}));
