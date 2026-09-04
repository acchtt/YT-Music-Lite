export type UpdateStatus = {
  configured: boolean;
  available: boolean;
  currentVersion: string;
  version?: string | null;
  notes?: string | null;
  publishedAt?: string | null;
  source?: string | null;
  message: string;
};

export type UpdateProgress = {
  downloaded: number;
  total?: number | null;
  finished: boolean;
  stage: "download" | "verify" | "install" | string;
};
