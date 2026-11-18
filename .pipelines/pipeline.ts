// Every push to main (one snapshot of the engine, published from the private repo it's developed in) builds the
// browser sample and deploys it as the public demo. Pull requests aren't built: the repo doesn't take them.
import { pipeline, steps } from "@olivervea/pipelines";
import { DEMO } from "./config";
import * as M from "./models";
import { CLOUDFLARE_API_TOKEN, GITHUB_TOKEN } from "./shared";
import { buildDemoStep } from "./steps/build-demo";

const source = steps.gitHubSource({
  name: "source",
  description: "Fetches the pushed commit from GitHub.",
  token: GITHUB_TOKEN,
});

const demo = steps.staticSite({ name: "demo", wasm: true });

export default pipeline({
  name: "Olve.Engine3D",
  description: "Build the engine's browser sample and deploy it as the public demo.",
  input: M.DeployRequest,
})
  .onPush(e => ({ repo: e.repository.full_name, sha: e.after }))
  .stage([source, from => ({ repo: from.input.repo, sha: from.input.sha })])
  .stage(buildDemoStep)
  .stage(demo.pages({
    name: "publish-demo",
    description: "Publishes the demo to Cloudflare Pages and checks it serves.",
    accountId: DEMO.accountId,
    project: DEMO.project,
    apiToken: CLOUDFLARE_API_TOKEN,
    link: DEMO.link,
  }));
