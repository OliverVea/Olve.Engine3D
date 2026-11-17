import { secret } from "@olivervea/pipelines";

export const GITHUB_TOKEN = secret("GITHUB_TOKEN", "GitHub token that reads this repository and manages its webhook.");
export const SSH_PRIVATE_KEY = secret("SSH_PRIVATE_KEY", "SSH key for ssh deploy targets: pulling the image and running helm on the host.");
