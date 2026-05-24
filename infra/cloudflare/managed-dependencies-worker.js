const OBJECT_KEY = "Managed.zip";

export default {
  async fetch(request, env) {
    const url = new URL(request.url);
    const token = url.searchParams.get("token");

    if (
      url.pathname !== `/${OBJECT_KEY}` ||
      (request.method !== "GET" && request.method !== "HEAD") ||
      !env.DOWNLOAD_TOKEN ||
      token !== env.DOWNLOAD_TOKEN.trim()
    ) {
      return new Response("Not found", { status: 404 });
    }

    const object = await env.DEPENDENCIES_BUCKET.get(OBJECT_KEY);
    if (!object) {
      return new Response("Not found", { status: 404 });
    }

    const headers = new Headers();
    object.writeHttpMetadata(headers);
    headers.set("etag", object.httpEtag);
    headers.set("content-type", "application/zip");
    headers.set("content-disposition", `attachment; filename="${OBJECT_KEY}"`);
    headers.set("cache-control", "private, max-age=300");

    return new Response(request.method === "HEAD" ? null : object.body, {
      headers,
    });
  },
};
