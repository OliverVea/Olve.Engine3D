import { secret } from "@olivervea/pipelines";

export const GITHUB_TOKEN = secret("GITHUB_TOKEN", "GitHub token that reads this repository and manages its webhook.");
export const CLOUDFLARE_API_TOKEN = secret("CLOUDFLARE_API_TOKEN", "Cloudflare API token with Account → Cloudflare Pages → Edit, for publishing the demo.");
