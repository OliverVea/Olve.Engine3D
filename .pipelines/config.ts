import { config, z } from "@olivervea/pipelines";

export const DEMO = config("demo", "Where the browser demo is published: a Cloudflare Pages project.", z.object({
  accountId: z.string().describe("The Cloudflare account that owns the Pages project."),
  project: z.string().describe("The Pages project; created on the first deploy."),
  link: z.string().optional().describe("The demo's public URL (the project's custom domain); unset = its pages.dev URL."),
}));
