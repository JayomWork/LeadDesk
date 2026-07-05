window.projectPulseAiWriting = {
  improve: async (url, request) => {
    const response = await fetch(url, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(request)
    });

    const payload = await response.json().catch(() => ({}));
    if (!response.ok) {
      throw new Error(payload.message || "Unable to generate AI suggestion.");
    }

    return payload.improvedText || "";
  },
  copy: async (text) => {
    await navigator.clipboard.writeText(text || "");
  }
};
