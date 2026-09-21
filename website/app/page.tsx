import { Ambient } from "@/components/ambient";
import { ArrowIcon, GitHubIcon, LINKS, Logo } from "@/components/brand";
import { CopyCommand } from "@/components/copy-command";
import { FeatureBento } from "@/components/feature-bento";
import { MatchRoom } from "@/components/match-room";
import { MotionLayer } from "@/components/motion-layer";
import { SiteHeader } from "@/components/site-header";
import { StackRow } from "@/components/stack-row";

const CLONE = `git clone ${LINKS.repo}.git`;

const NEEDS = [
  ["Host", "A Linux or Windows machine you control"],
  ["Database", "PostgreSQL, started by Docker"],
  ["Game", "One or more CS2 servers you already run"],
];

const COMPARE: [row: string, flicked: string, hosted: string, honest?: boolean][] = [
  ["Price", "Free, forever", "Free tier, paid subscriptions"],
  ["Source code", "Open. Read it, fork it", "Closed"],
  ["Who runs it", "You", "The vendor"],
  ["Server locations", "Wherever you put them", "The vendor's regions"],
  ["Rules, maps & formats", "Yours to decide", "The vendor's"],
  ["Player data", "In your own database", "In the vendor's database"],
  ["Anti-cheat", "Not included. Run your own", "Mature, kernel-level", true],
  ["Maturity", "Alpha", "Years in production", true],
];

const STACK = [
  ["Website", "Next.js & Tailwind"],
  ["Backend", "C# / .NET 10"],
  ["Database", "PostgreSQL"],
  ["Launcher", "Rust + Tauri"],
  ["On the server", "CounterStrikeSharp"],
];

const FAQ = [
  ["Is FLICKED really free?",
    "Yes. FLICKED is free and open source. Running it costs whatever your own hardware or VPS costs. There is no subscription, no premium tier and no per-player fee."],
  ["Is it affiliated with Valve or FACEIT?",
    "No. FLICKED is an independent community project. It is not affiliated with, endorsed by or connected to Valve Corporation or any existing competitive platform."],
  ["Does FLICKED include an anti-cheat?",
    "It does not, and one is not on the roadmap. Anti-cheat is a specialist field where the opposition works full time, so a system worth trusting takes a dedicated team and sustained investment. Shipping a weaker one would give players a sense of protection that isn't real. Server owners are free to run a community anti-cheat alongside FLICKED. In practice, FLICKED suits communities with known players, active admins, and recorded demos to review reports."],
  ["Does FLICKED create CS2 servers for me?",
    "Not automatically. You register the servers your community already runs, and FLICKED assigns a free one to each match and returns it to the pool when the match ends. Creating and destroying servers on demand is a separate infrastructure problem, and one this project cannot test properly at its current scale."],
  ["Is it ready for production?",
    "It's alpha. Expect things to change and break between releases. It suits communities happy to run early software and report what they find. It's not ready for a prize-money tournament this weekend."],
  ["What do I need to host it?",
    "A machine you control, Docker for PostgreSQL, and at least one CS2 server. The README on GitHub has the current setup steps."],
  ["How can I contribute?",
    "Open an issue, pick one up, or send a pull request. Right now, bug reports from real communities running real matches are the most valuable thing you can give."],
];

const FOOTER_NAV = [
  ["Project", [["GitHub", LINKS.repo], ["Releases", LINKS.releases], ["Issues", LINKS.issues], ["README", LINKS.readme]]],
  ["Platform", [["Features", "#features"], ["Self-host", "#self-host"], ["Compare", "#compare"], ["FAQ", "#faq"]]],
] as const;

function Seam() {
  return <div className="seam-neutral" aria-hidden="true" />;
}

export default function Home() {
  return (
    <>
      <a href="#main" className="sr-only focus:not-sr-only focus:fixed focus:left-4 focus:top-4 focus:z-[999] focus:rounded-md focus:bg-elevated focus:px-4 focus:py-2 focus:text-foreground">
        Skip to content
      </a>


      <div className="frame-rails" aria-hidden="true"><i /><i /></div>
      <Ambient />

      <SiteHeader />

      <main id="main">

        {/* ═══ HERO — just the statement, centred. The product follows below. ═══ */}
        <section data-tone="hero" className="mx-auto max-w-frame px-6 lg:px-10">
          <div className="hero-copy mx-auto max-w-3xl text-center">
            <p className="eyebrow" data-rv>Alpha · Free &amp; open source</p>

            <h1 className="display hero-h1 mt-6" data-rv>
              Competitive CS2.<br />On your servers.
            </h1>

            <p className="mx-auto mt-6 max-w-[46ch] text-[17px] leading-relaxed text-muted" data-rv>
              A free, open-source platform for running your own CS2 matchmaking:
              queues, parties, map vote, ratings, demos and stats. You bring the servers.
            </p>

            <div className="mt-9 flex flex-wrap items-center justify-center gap-3" data-rv>
              <a href="#self-host" className="btn btn-primary btn-lg"><span>Get started<ArrowIcon /></span></a>
              <a href={LINKS.repo} className="btn btn-outline btn-lg" target="_blank" rel="noreferrer"><span><GitHubIcon />GitHub</span></a>
            </div>
          </div>

          <div className="mx-auto max-w-5xl pb-24" data-rv-panel>
            <MatchRoom />
          </div>
        </section>

        {/* ═══ STACK ═══ */}
        <section data-tone="quiet" className="mx-auto max-w-frame px-6 py-14 lg:px-10">
          <StackRow />
        </section>

        <Seam />

        {/* ═══ FEATURES ═══ */}
        <section id="features" data-tone="orange" className="mx-auto max-w-frame px-6 py-24 lg:px-10 lg:py-32">
          <header className="grid items-end gap-6 lg:grid-cols-[1.2fr_1fr] lg:gap-16" data-rv>
            <div>
              <p className="eyebrow">Features</p>
              <h2 className="display h2 mt-6">Everything a pug night needs. <span className="accent">Nothing rented.</span></h2>
            </div>
            <p className="text-[16px] leading-relaxed text-muted lg:pb-2">
              Matchmaking, servers, rankings, demos and stats in one platform, so your
              community stops stitching together bots, spreadsheets and borrowed servers.
            </p>
          </header>

          <FeatureBento />
        </section>

        <Seam />

        {/* ═══ SELF-HOST ═══ */}
        <section id="self-host" data-tone="cool" className="mx-auto max-w-frame px-6 py-24 lg:px-10 lg:py-32">
          <div className="grid items-start gap-14 lg:grid-cols-[1fr_1.1fr]">
            <header data-rv>
              <p className="eyebrow">Self-host</p>
              <h2 className="display h2 mt-6">Your servers. Your community. <span className="accent">Your data.</span></h2>
              <p className="mt-6 max-w-[54ch] text-[16px] leading-relaxed text-muted">
                FLICKED runs on hardware you control, from a box under the desk to a rack in a
                datacenter near your players. Nobody else sets your rules, holds your
                players&apos; data, or decides when the lights go off.
              </p>

              <dl className="need mt-10 border-t border-white/[0.06] pt-7">
                {NEEDS.map(([k, v]) => (
                  <div key={k} className="contents">
                    <dt className="stat-k pt-[5px]">{k}</dt>
                    <dd className="text-[14.5px] text-foreground">{v}</dd>
                  </div>
                ))}
              </dl>
            </header>

            <div className="panel-2 overflow-hidden rounded-xl" data-rv-panel>
              <div className="flex items-center gap-2 border-b border-white/[0.06] px-5 py-3.5">
                <i className="h-2.5 w-2.5 rounded-full bg-white/10" /><i className="h-2.5 w-2.5 rounded-full bg-white/10" /><i className="h-2.5 w-2.5 rounded-full bg-white/10" />
                <span className="ms-3 font-mono text-[12px] text-subtle">~/flicked</span>
                <span className="ms-auto"><CopyCommand text={CLONE} /></span>
              </div>
              <div className="term overflow-x-auto p-6">
                <p className="whitespace-nowrap"><span className="p">$ </span><span className="text-foreground">{CLONE}</span></p>
                <p className="whitespace-nowrap"><span className="p">$ </span><span className="text-foreground">cd FLICKED</span></p>
                <p className="whitespace-nowrap"><span className="p">$ </span><span className="text-foreground">docker compose up -d</span></p>
                <p className="mt-3 c"># then register your CS2 servers and open the queue,</p>
                <p className="c"># following the setup guide in README.md</p>
              </div>
              <div className="grid border-t border-white/[0.06] sm:grid-cols-3 sm:divide-x sm:divide-white/[0.06]">
                {[["01", "Clone", "Grab the source from GitHub."], ["02", "Register", "Add the CS2 servers you already run."], ["03", "Play", "Open the queue to your community."]].map(([n, t, d]) => (
                  <div key={n} className="border-b border-white/[0.06] px-6 py-5 last:border-b-0 sm:border-b-0">
                    <p className="feature-n">{n}</p>
                    <p className="mt-1.5 font-display text-[19px] font-bold uppercase text-foreground">{t}</p>
                    <p className="mt-1 text-[13px] leading-relaxed text-subtle">{d}</p>
                  </div>
                ))}
              </div>
            </div>
          </div>
        </section>

        <Seam />

        {/* ═══ COMPARE ═══ */}
        <section id="compare" data-tone="amber" className="mx-auto max-w-frame px-6 py-24 lg:px-10 lg:py-32">
          <header className="max-w-2xl" data-rv>
            <p className="eyebrow">Compare</p>
            <h2 className="display h2 mt-6">What changes when you <span className="accent">own</span> the platform.</h2>
            <p className="mt-6 text-[16px] leading-relaxed text-muted">
              Hosted platforms do a lot well, especially anti-cheat. FLICKED is for
              communities that would rather have control. Here&apos;s the honest trade.
            </p>
          </header>

          <div className="panel-1 mt-14 overflow-hidden rounded-xl" data-rv-panel>
            <div className="cmp-row cmp-head border-b border-white/[0.06]">
              <span className="hidden sm:block" />
              <span className="flex items-center gap-2.5"><Logo size={22} /><span className="wordmark" style={{ fontSize: 20 }}>FLICKED</span></span>
              <span className="font-display text-[20px] font-bold uppercase text-subtle">Hosted platforms</span>
            </div>
            <div className="divide-y divide-white/[0.05]">
              {COMPARE.map(([row, flicked, hosted, honest]) => (
                <div key={row} className="cmp-row">
                  <span>{row}</span>
                  <span className={honest ? "honest" : "yes"}>{flicked}</span>
                  <span className="meh">{hosted}</span>
                </div>
              ))}
            </div>
          </div>
        </section>

        <Seam />

        {/* ═══ OPEN SOURCE ═══ */}
        <section id="open-source" data-tone="cool" className="mx-auto max-w-frame px-6 py-24 lg:px-10 lg:py-32">
          <div className="grid items-start gap-14 lg:grid-cols-[1fr_1fr]">
            <header data-rv>
              <p className="eyebrow">Open source</p>
              <h2 className="display h2 mt-6">Built in the open. <span className="accent">Break it with us.</span></h2>
              <p className="mt-6 max-w-[54ch] text-[16px] leading-relaxed text-muted">
                Every line of FLICKED is on GitHub. Read it, fork it, run it, send a pull
                request. It&apos;s alpha software: things will change and break. If something&apos;s
                off, that&apos;s worth reporting, not assuming it&apos;s expected.
              </p>
              <div className="mt-9 flex flex-wrap gap-4">
                <a href={LINKS.repo} className="btn btn-primary" target="_blank" rel="noreferrer"><span><GitHubIcon />Star on GitHub</span></a>
                <a href={LINKS.issues} className="btn btn-outline" target="_blank" rel="noreferrer"><span>Report an issue</span></a>
              </div>
            </header>

            <div className="panel-1 overflow-hidden rounded-xl" data-rv-panel>
              <div className="flex items-center justify-between border-b border-white/[0.06] px-6 py-4">
                <p className="stat-k">The stack</p>
                <span className="tag"><i className="tag-dot" />alpha</span>
              </div>
              <dl className="divide-y divide-white/[0.05]">
                {STACK.map(([k, v]) => (
                  <div key={k} className="flex items-baseline justify-between gap-6 px-6 py-4">
                    <dt className="text-[14px] text-subtle">{k}</dt>
                    <dd className={`font-display text-[19px] font-bold uppercase ${v.includes("planned") ? "text-faint" : "text-foreground"}`}>{v}</dd>
                  </div>
                ))}
              </dl>
            </div>
          </div>
        </section>

        <Seam />

        {/* ═══ FAQ ═══ */}
        <section id="faq" data-tone="orange" className="mx-auto max-w-frame px-6 py-24 lg:px-10 lg:py-32">
          <div className="grid gap-14 lg:grid-cols-[0.7fr_1fr]">
            <header data-rv>
              <p className="eyebrow">FAQ</p>
              <h2 className="display h2 mt-6">Before you spin one up.</h2>
              <p className="mt-6 text-[15.5px] leading-relaxed text-muted">
                Something missing?{" "}
                <a href={LINKS.issues} target="_blank" rel="noreferrer" className="text-primary-light underline decoration-primary/40 underline-offset-4 transition-colors hover:decoration-primary-light">Open an issue on GitHub</a>.
              </p>
            </header>

            {/* name="faq" keeps one open at a time, natively */}
            <div className="panel-1 divide-y divide-white/[0.06] overflow-hidden rounded-xl" id="faqList" data-rv-panel>
              {FAQ.map(([q, a], i) => (
                <details key={q} name="faq" open={i === 0}>
                  <summary>{q}<i className="sign" /></summary>
                  <div><p>{a}</p></div>
                </details>
              ))}
            </div>
          </div>
        </section>

        <Seam />

        {/* ═══ CTA ═══ */}
        <section id="get-started" data-tone="amber" className="mx-auto max-w-frame px-6 py-28 lg:px-10 lg:py-36">
          <div className="mx-auto max-w-3xl text-center" data-rv>
            <div className="flex justify-center"><Logo size={64} /></div>
            <h2 className="display mt-8 text-[clamp(2.8rem,6vw,4.8rem)] leading-[.92]">
              Your server.<br />Your rules. <span className="accent">Your ranks.</span>
            </h2>
            <p className="mx-auto mt-7 max-w-[54ch] text-[16.5px] leading-relaxed text-muted">
              Stop renting your community from someone else&apos;s platform. Clone FLICKED,
              run your first match, and tell us what broke.
            </p>
            <div className="mt-10 flex flex-wrap items-center justify-center gap-4">
              <a href={LINKS.repo} className="btn btn-primary btn-lg" target="_blank" rel="noreferrer"><span><GitHubIcon />Get FLICKED</span></a>
              <a href={LINKS.readme} className="btn btn-outline btn-lg" target="_blank" rel="noreferrer"><span>Read the README</span></a>
            </div>
            <p className="mt-8 font-mono text-[12px] text-faint">Free · Open source · Self-hosted · Alpha</p>
          </div>
        </section>

      </main>

      {/* ═══ FOOTER ═══ */}
      <footer className="border-t border-white/[0.06]">
        <div className="mx-auto max-w-frame px-6 pt-16 lg:px-10">
          <div className="grid gap-12 pb-14 lg:grid-cols-[1.4fr_1fr]">
            <div>
              <a href="#" className="inline-flex items-center gap-2.5" aria-label="FLICKED, back to top">
                <Logo size={28} />
                <span className="wordmark">FLICKED</span>
              </a>
              <p className="mt-5 max-w-[40ch] text-[14px] leading-relaxed text-subtle">
                A free, open-source, self-hostable competitive platform for CS2.
              </p>
              <p className="mt-6 flex items-center gap-2.5 font-mono text-[12px] text-subtle">
                <i className="tag-dot" />Status: alpha
              </p>
            </div>

            <nav className="grid grid-cols-2 gap-10" aria-label="Footer">
              {FOOTER_NAV.map(([heading, links]) => (
                <div key={heading}>
                  <p className="foot-h">{heading}</p>
                  {links.map(([label, href]) => (
                    <a
                      key={label}
                      href={href}
                      className="foot-a"
                      {...(href.startsWith("http") ? { target: "_blank", rel: "noreferrer" } : {})}
                    >
                      {label}
                    </a>
                  ))}
                </div>
              ))}
            </nav>
          </div>

          <div
            className="flex flex-wrap items-center justify-between gap-4 border-t border-white/[0.06] py-7 text-[12.5px] text-faint"
            style={{ paddingBottom: "calc(1.75rem + env(safe-area-inset-bottom,0px))" }}
          >
            <span>© 2026 FLICKED contributors.</span>
            <span>Not affiliated with Valve Corporation. Counter-Strike 2 is a trademark of Valve Corporation.</span>
          </div>
        </div>
      </footer>

      <MotionLayer />
    </>
  );
}
