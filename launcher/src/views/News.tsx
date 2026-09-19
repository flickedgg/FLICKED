import { RECENT } from "../data/demo";

export default function News() {
  return (
    <div className="view">
      <header className="view-head">
        <p className="eyebrow">News</p>
        <h1 className="display h1">
          Latest <span className="accent">News</span>
        </h1>
      </header>
      <div className="NewsCards"></div>
    </div>
  );
}
