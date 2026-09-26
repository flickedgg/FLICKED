/* The launcher, running.

   Deliberately not autoplaying. It is a four megabyte download that says
   nothing until somebody wants to watch it, the sound matters (the queue and the
   map vote are worth hearing), and the site gates every animation behind the
   visitor's motion preference — a video that starts itself would be the one
   thing on the page ignoring that.

   The frame is reserved at the video's own 16:9, so nothing below it moves when
   the file arrives. preload="metadata" fetches a few kilobytes of header rather
   than the whole thing, which is what lets the browser show a duration and a
   scrubber before anyone presses play. */
export function PreviewVideo() {
  return (
    <figure className="mt-12" data-rv>
      <div className="panel-1 overflow-hidden rounded-xl">
        {/* No poster: there is no still to use, and pointing at one that does
            not exist costs a failed request and shows the same black frame the
            browser would have shown anyway. Worth adding when there is a
            frame worth showing. */}
        <video
          className="block aspect-video w-full bg-black"
          src="/flicked-preview.mp4"
          controls
          playsInline
          preload="metadata"
          width={1920}
          height={1080}
        >
          {/* Shown only where the element is unsupported, so it is a way out
              rather than a message: the file is still worth offering. */}
          <a href="/flicked-preview.mp4">Download the preview instead</a>
        </video>
      </div>

      <figcaption className="mt-4 text-[14px] text-muted">
        Queue, accept, map vote, and the game connecting itself — recorded on a
        live server, not a mockup.
      </figcaption>
    </figure>
  );
}
