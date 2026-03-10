(() => {
  const parseList = (value) => {
    if (!value) return [];
    return value
      .split(",")
      .map((x) => x.trim())
      .filter((x) => x.length > 0);
  };

  const escapeId = (id) => {
    if (typeof CSS !== "undefined" && typeof CSS.escape === "function") {
      return CSS.escape(id);
    }
    return id.replace(/[^a-zA-Z0-9_-]/g, "\\$&");
  };

  const applyOpenState = (root, openIds) => {
    for (const id of openIds) {
      const list = root.querySelector(`#${escapeId(id)}`);
      if (!list) continue;
      list.classList.add("show");

      const toggle = root.querySelector(`[href="#${escapeId(id)}"][data-bs-toggle="collapse"]`);
      if (toggle) {
        toggle.setAttribute("aria-expanded", "true");
        toggle.classList.remove("collapsed");
      }
    }
  };

  const applyActiveState = (root, activeItemIds) => {
    for (const id of activeItemIds) {
      const item = root.querySelector(`#${escapeId(id)}`);
      if (item) {
        item.classList.add("active");
      }
    }
  };

  const loadMenu = async (host) => {
    const partialUrl = host.dataset.lunetMenuPartial;
    if (!partialUrl) return;

    const openIds = parseList(host.dataset.lunetMenuOpen);
    const activeIds = parseList(host.dataset.lunetMenuActive);

    try {
      const res = await fetch(partialUrl, { cache: "force-cache" });
      if (!res.ok) return;

      host.innerHTML = await res.text();
      applyOpenState(host, openIds);
      applyActiveState(host, activeIds);
    } catch {
      // Keep fallback HTML (no-JS / offline / transient errors).
    }
  };

  const init = () => {
    const menus = document.querySelectorAll(".lunet-menu-async[data-lunet-menu-partial]");
    for (const host of menus) {
      void loadMenu(host);
    }
  };

  if (document.readyState === "loading") {
    document.addEventListener("DOMContentLoaded", init);
  } else {
    init();
  }
})();

(() => {
  "use strict";

  function normalizeKind(text) {
    var kind = (text || "").trim().toLowerCase();
    switch (kind) {
      case "ctor":
      case "constructor":
      case "constructors":
        return "constructor";
      case "field":
      case "fields":
        return "field";
      case "prop":
      case "property":
      case "properties":
        return "property";
      case "method":
      case "methods":
        return "method";
      case "event":
      case "events":
        return "event";
      case "operator":
      case "operators":
        return "operator";
      case "extension":
      case "extensions":
        return "extension";
      case "eii":
      case "eiimethod":
      case "explicit":
        return "eiimethod";
      default:
        return kind;
    }
  }

  function parseQuery(query) {
    var tokens = (query || "")
      .trim()
      .split(/\s+/)
      .filter(Boolean);

    var kinds = [];
    var textTokens = [];

    for (var index = 0; index < tokens.length; index++) {
      var token = tokens[index];
      var idx = token.indexOf(":");
      if (idx > 0) {
        var key = token.slice(0, idx).toLowerCase();
        var value = token.slice(idx + 1);
        if (key === "kind" && value) {
          kinds.push(normalizeKind(value));
          continue;
        }
      }
      textTokens.push(token);
    }

    return {
      kinds,
      text: textTokens.join(" ").toLowerCase(),
    };
  }

  function setupFilter(filterRoot) {
    var input = filterRoot.querySelector(".api-dotnet-member-filter-input");
    if (!input) return;

    var scope = filterRoot.closest(".api-dotnet") || document;
    var items = scope.querySelectorAll(".api-dotnet-member-item");
    var itemCount = items.length;
    if (itemCount === 0) return;

    for (var index = 0; index < itemCount; index++) {
      var item = items[index];
      if (!item.dataset.apiSearchText) {
        item.dataset.apiSearchText = (item.textContent || "").toLowerCase();
      }
      if (!item.dataset.apiKind && item.getAttribute("data-api-kind")) {
        item.dataset.apiKind = normalizeKind(item.getAttribute("data-api-kind"));
      } else if (item.dataset.apiKind) {
        item.dataset.apiKind = normalizeKind(item.dataset.apiKind);
      }
    }

    var status = filterRoot.querySelector(".api-dotnet-member-filter-status");
    if (!status) {
      status = document.createElement("div");
      status.className = "api-dotnet-member-filter-status";
      status.setAttribute("aria-live", "polite");
      filterRoot.appendChild(status);
    }

    function apply() {
      var q = parseQuery(input.value);
      var visible = 0;

      for (var index = 0; index < itemCount; index++) {
        var item = items[index];
        var kindOk =
          q.kinds.length === 0 ||
          q.kinds.includes(item.dataset.apiKind || "");
        var textOk =
          q.text === "" || (item.dataset.apiSearchText || "").includes(q.text);
        var show = kindOk && textOk;
        item.hidden = !show;
        if (show) visible++;
      }

      status.textContent =
        q.kinds.length === 0 && q.text === ""
          ? ""
          : visible + " / " + itemCount + " shown";
    }

    input.addEventListener("input", apply, { passive: true });
    input.addEventListener("keydown", function (ev) {
      if (ev.key === "Escape") {
        input.value = "";
        apply();
      }
    });

    apply();
  }

  function init() {
    var filters = document.querySelectorAll("[data-api-dotnet-filter]");
    for (var index = 0; index < filters.length; index++) {
      setupFilter(filters[index]);
    }
  }

  if (document.readyState === "loading") {
    document.addEventListener("DOMContentLoaded", init, { once: true });
  } else {
    init();
  }
})();

/*!
  * Bootstrap v5.3.8 (https://getbootstrap.com/)
  * Copyright 2011-2025 The Bootstrap Authors (https://github.com/twbs/bootstrap/graphs/contributors)
  * Licensed under MIT (https://github.com/twbs/bootstrap/blob/main/LICENSE)
  */
!function(t,e){"object"==typeof exports&&"undefined"!=typeof module?module.exports=e():"function"==typeof define&&define.amd?define(e):(t="undefined"!=typeof globalThis?globalThis:t||self).bootstrap=e()}(this,function(){"use strict";const t=new Map,e={set(e,i,n){t.has(e)||t.set(e,new Map);const s=t.get(e);s.has(i)||0===s.size?s.set(i,n):console.error(`Bootstrap doesn't allow more than one instance per element. Bound instance: ${Array.from(s.keys())[0]}.`)},get:(e,i)=>t.has(e)&&t.get(e).get(i)||null,remove(e,i){if(!t.has(e))return;const n=t.get(e);n.delete(i),0===n.size&&t.delete(e)}},i="transitionend",n=t=>(t&&window.CSS&&window.CSS.escape&&(t=t.replace(/#([^\s"#']+)/g,(t,e)=>`#${CSS.escape(e)}`)),t),s=t=>null==t?`${t}`:Object.prototype.toString.call(t).match(/\s([a-z]+)/i)[1].toLowerCase(),o=t=>{t.dispatchEvent(new Event(i))},r=t=>!(!t||"object"!=typeof t)&&(void 0!==t.jquery&&(t=t[0]),void 0!==t.nodeType),a=t=>r(t)?t.jquery?t[0]:t:"string"==typeof t&&t.length>0?document.querySelector(n(t)):null,l=t=>{if(!r(t)||0===t.getClientRects().length)return!1;const e="visible"===getComputedStyle(t).getPropertyValue("visibility"),i=t.closest("details:not([open])");if(!i)return e;if(i!==t){const e=t.closest("summary");if(e&&e.parentNode!==i)return!1;if(null===e)return!1}return e},c=t=>!t||t.nodeType!==Node.ELEMENT_NODE||!!t.classList.contains("disabled")||(void 0!==t.disabled?t.disabled:t.hasAttribute("disabled")&&"false"!==t.getAttribute("disabled")),h=t=>{if(!document.documentElement.attachShadow)return null;if("function"==typeof t.getRootNode){const e=t.getRootNode();return e instanceof ShadowRoot?e:null}return t instanceof ShadowRoot?t:t.parentNode?h(t.parentNode):null},d=()=>{},u=t=>{t.offsetHeight},f=()=>window.jQuery&&!document.body.hasAttribute("data-bs-no-jquery")?window.jQuery:null,p=[],m=()=>"rtl"===document.documentElement.dir,g=t=>{var e;e=()=>{const e=f();if(e){const i=t.NAME,n=e.fn[i];e.fn[i]=t.jQueryInterface,e.fn[i].Constructor=t,e.fn[i].noConflict=()=>(e.fn[i]=n,t.jQueryInterface)}},"loading"===document.readyState?(p.length||document.addEventListener("DOMContentLoaded",()=>{for(const t of p)t()}),p.push(e)):e()},_=(t,e=[],i=t)=>"function"==typeof t?t.call(...e):i,b=(t,e,n=!0)=>{if(!n)return void _(t);const s=(t=>{if(!t)return 0;let{transitionDuration:e,transitionDelay:i}=window.getComputedStyle(t);const n=Number.parseFloat(e),s=Number.parseFloat(i);return n||s?(e=e.split(",")[0],i=i.split(",")[0],1e3*(Number.parseFloat(e)+Number.parseFloat(i))):0})(e)+5;let r=!1;const a=({target:n})=>{n===e&&(r=!0,e.removeEventListener(i,a),_(t))};e.addEventListener(i,a),setTimeout(()=>{r||o(e)},s)},v=(t,e,i,n)=>{const s=t.length;let o=t.indexOf(e);return-1===o?!i&&n?t[s-1]:t[0]:(o+=i?1:-1,n&&(o=(o+s)%s),t[Math.max(0,Math.min(o,s-1))])},y=/[^.]*(?=\..*)\.|.*/,w=/\..*/,A=/::\d+$/,E={};let T=1;const C={mouseenter:"mouseover",mouseleave:"mouseout"},O=new Set(["click","dblclick","mouseup","mousedown","contextmenu","mousewheel","DOMMouseScroll","mouseover","mouseout","mousemove","selectstart","selectend","keydown","keypress","keyup","orientationchange","touchstart","touchmove","touchend","touchcancel","pointerdown","pointermove","pointerup","pointerleave","pointercancel","gesturestart","gesturechange","gestureend","focus","blur","change","reset","select","submit","focusin","focusout","load","unload","beforeunload","resize","move","DOMContentLoaded","readystatechange","error","abort","scroll"]);function x(t,e){return e&&`${e}::${T++}`||t.uidEvent||T++}function k(t){const e=x(t);return t.uidEvent=e,E[e]=E[e]||{},E[e]}function L(t,e,i=null){return Object.values(t).find(t=>t.callable===e&&t.delegationSelector===i)}function S(t,e,i){const n="string"==typeof e,s=n?i:e||i;let o=N(t);return O.has(o)||(o=t),[n,s,o]}function D(t,e,i,n,s){if("string"!=typeof e||!t)return;let[o,r,a]=S(e,i,n);if(e in C){const t=t=>function(e){if(!e.relatedTarget||e.relatedTarget!==e.delegateTarget&&!e.delegateTarget.contains(e.relatedTarget))return t.call(this,e)};r=t(r)}const l=k(t),c=l[a]||(l[a]={}),h=L(c,r,o?i:null);if(h)return void(h.oneOff=h.oneOff&&s);const d=x(r,e.replace(y,"")),u=o?function(t,e,i){return function n(s){const o=t.querySelectorAll(e);for(let{target:r}=s;r&&r!==this;r=r.parentNode)for(const a of o)if(a===r)return j(s,{delegateTarget:r}),n.oneOff&&P.off(t,s.type,e,i),i.apply(r,[s])}}(t,i,r):function(t,e){return function i(n){return j(n,{delegateTarget:t}),i.oneOff&&P.off(t,n.type,e),e.apply(t,[n])}}(t,r);u.delegationSelector=o?i:null,u.callable=r,u.oneOff=s,u.uidEvent=d,c[d]=u,t.addEventListener(a,u,o)}function $(t,e,i,n,s){const o=L(e[i],n,s);o&&(t.removeEventListener(i,o,Boolean(s)),delete e[i][o.uidEvent])}function I(t,e,i,n){const s=e[i]||{};for(const[o,r]of Object.entries(s))o.includes(n)&&$(t,e,i,r.callable,r.delegationSelector)}function N(t){return t=t.replace(w,""),C[t]||t}const P={on(t,e,i,n){D(t,e,i,n,!1)},one(t,e,i,n){D(t,e,i,n,!0)},off(t,e,i,n){if("string"!=typeof e||!t)return;const[s,o,r]=S(e,i,n),a=r!==e,l=k(t),c=l[r]||{},h=e.startsWith(".");if(void 0===o){if(h)for(const i of Object.keys(l))I(t,l,i,e.slice(1));for(const[i,n]of Object.entries(c)){const s=i.replace(A,"");a&&!e.includes(s)||$(t,l,r,n.callable,n.delegationSelector)}}else{if(!Object.keys(c).length)return;$(t,l,r,o,s?i:null)}},trigger(t,e,i){if("string"!=typeof e||!t)return null;const n=f();let s=null,o=!0,r=!0,a=!1;e!==N(e)&&n&&(s=n.Event(e,i),n(t).trigger(s),o=!s.isPropagationStopped(),r=!s.isImmediatePropagationStopped(),a=s.isDefaultPrevented());const l=j(new Event(e,{bubbles:o,cancelable:!0}),i);return a&&l.preventDefault(),r&&t.dispatchEvent(l),l.defaultPrevented&&s&&s.preventDefault(),l}};function j(t,e={}){for(const[i,n]of Object.entries(e))try{t[i]=n}catch(e){Object.defineProperty(t,i,{configurable:!0,get:()=>n})}return t}function M(t){if("true"===t)return!0;if("false"===t)return!1;if(t===Number(t).toString())return Number(t);if(""===t||"null"===t)return null;if("string"!=typeof t)return t;try{return JSON.parse(decodeURIComponent(t))}catch(e){return t}}function F(t){return t.replace(/[A-Z]/g,t=>`-${t.toLowerCase()}`)}const H={setDataAttribute(t,e,i){t.setAttribute(`data-bs-${F(e)}`,i)},removeDataAttribute(t,e){t.removeAttribute(`data-bs-${F(e)}`)},getDataAttributes(t){if(!t)return{};const e={},i=Object.keys(t.dataset).filter(t=>t.startsWith("bs")&&!t.startsWith("bsConfig"));for(const n of i){let i=n.replace(/^bs/,"");i=i.charAt(0).toLowerCase()+i.slice(1),e[i]=M(t.dataset[n])}return e},getDataAttribute:(t,e)=>M(t.getAttribute(`data-bs-${F(e)}`))};class W{static get Default(){return{}}static get DefaultType(){return{}}static get NAME(){throw new Error('You have to implement the static method "NAME", for each component!')}_getConfig(t){return t=this._mergeConfigObj(t),t=this._configAfterMerge(t),this._typeCheckConfig(t),t}_configAfterMerge(t){return t}_mergeConfigObj(t,e){const i=r(e)?H.getDataAttribute(e,"config"):{};return{...this.constructor.Default,..."object"==typeof i?i:{},...r(e)?H.getDataAttributes(e):{},..."object"==typeof t?t:{}}}_typeCheckConfig(t,e=this.constructor.DefaultType){for(const[i,n]of Object.entries(e)){const e=t[i],o=r(e)?"element":s(e);if(!new RegExp(n).test(o))throw new TypeError(`${this.constructor.NAME.toUpperCase()}: Option "${i}" provided type "${o}" but expected type "${n}".`)}}}class B extends W{constructor(t,i){super(),(t=a(t))&&(this._element=t,this._config=this._getConfig(i),e.set(this._element,this.constructor.DATA_KEY,this))}dispose(){e.remove(this._element,this.constructor.DATA_KEY),P.off(this._element,this.constructor.EVENT_KEY);for(const t of Object.getOwnPropertyNames(this))this[t]=null}_queueCallback(t,e,i=!0){b(t,e,i)}_getConfig(t){return t=this._mergeConfigObj(t,this._element),t=this._configAfterMerge(t),this._typeCheckConfig(t),t}static getInstance(t){return e.get(a(t),this.DATA_KEY)}static getOrCreateInstance(t,e={}){return this.getInstance(t)||new this(t,"object"==typeof e?e:null)}static get VERSION(){return"5.3.8"}static get DATA_KEY(){return`bs.${this.NAME}`}static get EVENT_KEY(){return`.${this.DATA_KEY}`}static eventName(t){return`${t}${this.EVENT_KEY}`}}const z=t=>{let e=t.getAttribute("data-bs-target");if(!e||"#"===e){let i=t.getAttribute("href");if(!i||!i.includes("#")&&!i.startsWith("."))return null;i.includes("#")&&!i.startsWith("#")&&(i=`#${i.split("#")[1]}`),e=i&&"#"!==i?i.trim():null}return e?e.split(",").map(t=>n(t)).join(","):null},R={find:(t,e=document.documentElement)=>[].concat(...Element.prototype.querySelectorAll.call(e,t)),findOne:(t,e=document.documentElement)=>Element.prototype.querySelector.call(e,t),children:(t,e)=>[].concat(...t.children).filter(t=>t.matches(e)),parents(t,e){const i=[];let n=t.parentNode.closest(e);for(;n;)i.push(n),n=n.parentNode.closest(e);return i},prev(t,e){let i=t.previousElementSibling;for(;i;){if(i.matches(e))return[i];i=i.previousElementSibling}return[]},next(t,e){let i=t.nextElementSibling;for(;i;){if(i.matches(e))return[i];i=i.nextElementSibling}return[]},focusableChildren(t){const e=["a","button","input","textarea","select","details","[tabindex]",'[contenteditable="true"]'].map(t=>`${t}:not([tabindex^="-"])`).join(",");return this.find(e,t).filter(t=>!c(t)&&l(t))},getSelectorFromElement(t){const e=z(t);return e&&R.findOne(e)?e:null},getElementFromSelector(t){const e=z(t);return e?R.findOne(e):null},getMultipleElementsFromSelector(t){const e=z(t);return e?R.find(e):[]}},q=(t,e="hide")=>{const i=`click.dismiss${t.EVENT_KEY}`,n=t.NAME;P.on(document,i,`[data-bs-dismiss="${n}"]`,function(i){if(["A","AREA"].includes(this.tagName)&&i.preventDefault(),c(this))return;const s=R.getElementFromSelector(this)||this.closest(`.${n}`);t.getOrCreateInstance(s)[e]()})},V=".bs.alert",K=`close${V}`,Q=`closed${V}`;class X extends B{static get NAME(){return"alert"}close(){if(P.trigger(this._element,K).defaultPrevented)return;this._element.classList.remove("show");const t=this._element.classList.contains("fade");this._queueCallback(()=>this._destroyElement(),this._element,t)}_destroyElement(){this._element.remove(),P.trigger(this._element,Q),this.dispose()}static jQueryInterface(t){return this.each(function(){const e=X.getOrCreateInstance(this);if("string"==typeof t){if(void 0===e[t]||t.startsWith("_")||"constructor"===t)throw new TypeError(`No method named "${t}"`);e[t](this)}})}}q(X,"close"),g(X);const Y='[data-bs-toggle="button"]';class U extends B{static get NAME(){return"button"}toggle(){this._element.setAttribute("aria-pressed",this._element.classList.toggle("active"))}static jQueryInterface(t){return this.each(function(){const e=U.getOrCreateInstance(this);"toggle"===t&&e[t]()})}}P.on(document,"click.bs.button.data-api",Y,t=>{t.preventDefault();const e=t.target.closest(Y);U.getOrCreateInstance(e).toggle()}),g(U);const G=".bs.swipe",J=`touchstart${G}`,Z=`touchmove${G}`,tt=`touchend${G}`,et=`pointerdown${G}`,it=`pointerup${G}`,nt={endCallback:null,leftCallback:null,rightCallback:null},st={endCallback:"(function|null)",leftCallback:"(function|null)",rightCallback:"(function|null)"};class ot extends W{constructor(t,e){super(),this._element=t,t&&ot.isSupported()&&(this._config=this._getConfig(e),this._deltaX=0,this._supportPointerEvents=Boolean(window.PointerEvent),this._initEvents())}static get Default(){return nt}static get DefaultType(){return st}static get NAME(){return"swipe"}dispose(){P.off(this._element,G)}_start(t){this._supportPointerEvents?this._eventIsPointerPenTouch(t)&&(this._deltaX=t.clientX):this._deltaX=t.touches[0].clientX}_end(t){this._eventIsPointerPenTouch(t)&&(this._deltaX=t.clientX-this._deltaX),this._handleSwipe(),_(this._config.endCallback)}_move(t){this._deltaX=t.touches&&t.touches.length>1?0:t.touches[0].clientX-this._deltaX}_handleSwipe(){const t=Math.abs(this._deltaX);if(t<=40)return;const e=t/this._deltaX;this._deltaX=0,e&&_(e>0?this._config.rightCallback:this._config.leftCallback)}_initEvents(){this._supportPointerEvents?(P.on(this._element,et,t=>this._start(t)),P.on(this._element,it,t=>this._end(t)),this._element.classList.add("pointer-event")):(P.on(this._element,J,t=>this._start(t)),P.on(this._element,Z,t=>this._move(t)),P.on(this._element,tt,t=>this._end(t)))}_eventIsPointerPenTouch(t){return this._supportPointerEvents&&("pen"===t.pointerType||"touch"===t.pointerType)}static isSupported(){return"ontouchstart"in document.documentElement||navigator.maxTouchPoints>0}}const rt=".bs.carousel",at=".data-api",lt="ArrowLeft",ct="ArrowRight",ht="next",dt="prev",ut="left",ft="right",pt=`slide${rt}`,mt=`slid${rt}`,gt=`keydown${rt}`,_t=`mouseenter${rt}`,bt=`mouseleave${rt}`,vt=`dragstart${rt}`,yt=`load${rt}${at}`,wt=`click${rt}${at}`,At="carousel",Et="active",Tt=".active",Ct=".carousel-item",Ot=Tt+Ct,xt={[lt]:ft,[ct]:ut},kt={interval:5e3,keyboard:!0,pause:"hover",ride:!1,touch:!0,wrap:!0},Lt={interval:"(number|boolean)",keyboard:"boolean",pause:"(string|boolean)",ride:"(boolean|string)",touch:"boolean",wrap:"boolean"};class St extends B{constructor(t,e){super(t,e),this._interval=null,this._activeElement=null,this._isSliding=!1,this.touchTimeout=null,this._swipeHelper=null,this._indicatorsElement=R.findOne(".carousel-indicators",this._element),this._addEventListeners(),this._config.ride===At&&this.cycle()}static get Default(){return kt}static get DefaultType(){return Lt}static get NAME(){return"carousel"}next(){this._slide(ht)}nextWhenVisible(){!document.hidden&&l(this._element)&&this.next()}prev(){this._slide(dt)}pause(){this._isSliding&&o(this._element),this._clearInterval()}cycle(){this._clearInterval(),this._updateInterval(),this._interval=setInterval(()=>this.nextWhenVisible(),this._config.interval)}_maybeEnableCycle(){this._config.ride&&(this._isSliding?P.one(this._element,mt,()=>this.cycle()):this.cycle())}to(t){const e=this._getItems();if(t>e.length-1||t<0)return;if(this._isSliding)return void P.one(this._element,mt,()=>this.to(t));const i=this._getItemIndex(this._getActive());if(i===t)return;const n=t>i?ht:dt;this._slide(n,e[t])}dispose(){this._swipeHelper&&this._swipeHelper.dispose(),super.dispose()}_configAfterMerge(t){return t.defaultInterval=t.interval,t}_addEventListeners(){this._config.keyboard&&P.on(this._element,gt,t=>this._keydown(t)),"hover"===this._config.pause&&(P.on(this._element,_t,()=>this.pause()),P.on(this._element,bt,()=>this._maybeEnableCycle())),this._config.touch&&ot.isSupported()&&this._addTouchEventListeners()}_addTouchEventListeners(){for(const t of R.find(".carousel-item img",this._element))P.on(t,vt,t=>t.preventDefault());const t={leftCallback:()=>this._slide(this._directionToOrder(ut)),rightCallback:()=>this._slide(this._directionToOrder(ft)),endCallback:()=>{"hover"===this._config.pause&&(this.pause(),this.touchTimeout&&clearTimeout(this.touchTimeout),this.touchTimeout=setTimeout(()=>this._maybeEnableCycle(),500+this._config.interval))}};this._swipeHelper=new ot(this._element,t)}_keydown(t){if(/input|textarea/i.test(t.target.tagName))return;const e=xt[t.key];e&&(t.preventDefault(),this._slide(this._directionToOrder(e)))}_getItemIndex(t){return this._getItems().indexOf(t)}_setActiveIndicatorElement(t){if(!this._indicatorsElement)return;const e=R.findOne(Tt,this._indicatorsElement);e.classList.remove(Et),e.removeAttribute("aria-current");const i=R.findOne(`[data-bs-slide-to="${t}"]`,this._indicatorsElement);i&&(i.classList.add(Et),i.setAttribute("aria-current","true"))}_updateInterval(){const t=this._activeElement||this._getActive();if(!t)return;const e=Number.parseInt(t.getAttribute("data-bs-interval"),10);this._config.interval=e||this._config.defaultInterval}_slide(t,e=null){if(this._isSliding)return;const i=this._getActive(),n=t===ht,s=e||v(this._getItems(),i,n,this._config.wrap);if(s===i)return;const o=this._getItemIndex(s),r=e=>P.trigger(this._element,e,{relatedTarget:s,direction:this._orderToDirection(t),from:this._getItemIndex(i),to:o});if(r(pt).defaultPrevented)return;if(!i||!s)return;const a=Boolean(this._interval);this.pause(),this._isSliding=!0,this._setActiveIndicatorElement(o),this._activeElement=s;const l=n?"carousel-item-start":"carousel-item-end",c=n?"carousel-item-next":"carousel-item-prev";s.classList.add(c),u(s),i.classList.add(l),s.classList.add(l),this._queueCallback(()=>{s.classList.remove(l,c),s.classList.add(Et),i.classList.remove(Et,c,l),this._isSliding=!1,r(mt)},i,this._isAnimated()),a&&this.cycle()}_isAnimated(){return this._element.classList.contains("slide")}_getActive(){return R.findOne(Ot,this._element)}_getItems(){return R.find(Ct,this._element)}_clearInterval(){this._interval&&(clearInterval(this._interval),this._interval=null)}_directionToOrder(t){return m()?t===ut?dt:ht:t===ut?ht:dt}_orderToDirection(t){return m()?t===dt?ut:ft:t===dt?ft:ut}static jQueryInterface(t){return this.each(function(){const e=St.getOrCreateInstance(this,t);if("number"!=typeof t){if("string"==typeof t){if(void 0===e[t]||t.startsWith("_")||"constructor"===t)throw new TypeError(`No method named "${t}"`);e[t]()}}else e.to(t)})}}P.on(document,wt,"[data-bs-slide], [data-bs-slide-to]",function(t){const e=R.getElementFromSelector(this);if(!e||!e.classList.contains(At))return;t.preventDefault();const i=St.getOrCreateInstance(e),n=this.getAttribute("data-bs-slide-to");return n?(i.to(n),void i._maybeEnableCycle()):"next"===H.getDataAttribute(this,"slide")?(i.next(),void i._maybeEnableCycle()):(i.prev(),void i._maybeEnableCycle())}),P.on(window,yt,()=>{const t=R.find('[data-bs-ride="carousel"]');for(const e of t)St.getOrCreateInstance(e)}),g(St);const Dt=".bs.collapse",$t=`show${Dt}`,It=`shown${Dt}`,Nt=`hide${Dt}`,Pt=`hidden${Dt}`,jt=`click${Dt}.data-api`,Mt="show",Ft="collapse",Ht="collapsing",Wt=`:scope .${Ft} .${Ft}`,Bt='[data-bs-toggle="collapse"]',zt={parent:null,toggle:!0},Rt={parent:"(null|element)",toggle:"boolean"};class qt extends B{constructor(t,e){super(t,e),this._isTransitioning=!1,this._triggerArray=[];const i=R.find(Bt);for(const t of i){const e=R.getSelectorFromElement(t),i=R.find(e).filter(t=>t===this._element);null!==e&&i.length&&this._triggerArray.push(t)}this._initializeChildren(),this._config.parent||this._addAriaAndCollapsedClass(this._triggerArray,this._isShown()),this._config.toggle&&this.toggle()}static get Default(){return zt}static get DefaultType(){return Rt}static get NAME(){return"collapse"}toggle(){this._isShown()?this.hide():this.show()}show(){if(this._isTransitioning||this._isShown())return;let t=[];if(this._config.parent&&(t=this._getFirstLevelChildren(".collapse.show, .collapse.collapsing").filter(t=>t!==this._element).map(t=>qt.getOrCreateInstance(t,{toggle:!1}))),t.length&&t[0]._isTransitioning)return;if(P.trigger(this._element,$t).defaultPrevented)return;for(const e of t)e.hide();const e=this._getDimension();this._element.classList.remove(Ft),this._element.classList.add(Ht),this._element.style[e]=0,this._addAriaAndCollapsedClass(this._triggerArray,!0),this._isTransitioning=!0;const i=`scroll${e[0].toUpperCase()+e.slice(1)}`;this._queueCallback(()=>{this._isTransitioning=!1,this._element.classList.remove(Ht),this._element.classList.add(Ft,Mt),this._element.style[e]="",P.trigger(this._element,It)},this._element,!0),this._element.style[e]=`${this._element[i]}px`}hide(){if(this._isTransitioning||!this._isShown())return;if(P.trigger(this._element,Nt).defaultPrevented)return;const t=this._getDimension();this._element.style[t]=`${this._element.getBoundingClientRect()[t]}px`,u(this._element),this._element.classList.add(Ht),this._element.classList.remove(Ft,Mt);for(const t of this._triggerArray){const e=R.getElementFromSelector(t);e&&!this._isShown(e)&&this._addAriaAndCollapsedClass([t],!1)}this._isTransitioning=!0,this._element.style[t]="",this._queueCallback(()=>{this._isTransitioning=!1,this._element.classList.remove(Ht),this._element.classList.add(Ft),P.trigger(this._element,Pt)},this._element,!0)}_isShown(t=this._element){return t.classList.contains(Mt)}_configAfterMerge(t){return t.toggle=Boolean(t.toggle),t.parent=a(t.parent),t}_getDimension(){return this._element.classList.contains("collapse-horizontal")?"width":"height"}_initializeChildren(){if(!this._config.parent)return;const t=this._getFirstLevelChildren(Bt);for(const e of t){const t=R.getElementFromSelector(e);t&&this._addAriaAndCollapsedClass([e],this._isShown(t))}}_getFirstLevelChildren(t){const e=R.find(Wt,this._config.parent);return R.find(t,this._config.parent).filter(t=>!e.includes(t))}_addAriaAndCollapsedClass(t,e){if(t.length)for(const i of t)i.classList.toggle("collapsed",!e),i.setAttribute("aria-expanded",e)}static jQueryInterface(t){const e={};return"string"==typeof t&&/show|hide/.test(t)&&(e.toggle=!1),this.each(function(){const i=qt.getOrCreateInstance(this,e);if("string"==typeof t){if(void 0===i[t])throw new TypeError(`No method named "${t}"`);i[t]()}})}}P.on(document,jt,Bt,function(t){("A"===t.target.tagName||t.delegateTarget&&"A"===t.delegateTarget.tagName)&&t.preventDefault();for(const t of R.getMultipleElementsFromSelector(this))qt.getOrCreateInstance(t,{toggle:!1}).toggle()}),g(qt);var Vt="top",Kt="bottom",Qt="right",Xt="left",Yt="auto",Ut=[Vt,Kt,Qt,Xt],Gt="start",Jt="end",Zt="clippingParents",te="viewport",ee="popper",ie="reference",ne=Ut.reduce(function(t,e){return t.concat([e+"-"+Gt,e+"-"+Jt])},[]),se=[].concat(Ut,[Yt]).reduce(function(t,e){return t.concat([e,e+"-"+Gt,e+"-"+Jt])},[]),oe="beforeRead",re="read",ae="afterRead",le="beforeMain",ce="main",he="afterMain",de="beforeWrite",ue="write",fe="afterWrite",pe=[oe,re,ae,le,ce,he,de,ue,fe];function me(t){return t?(t.nodeName||"").toLowerCase():null}function ge(t){if(null==t)return window;if("[object Window]"!==t.toString()){var e=t.ownerDocument;return e&&e.defaultView||window}return t}function _e(t){return t instanceof ge(t).Element||t instanceof Element}function be(t){return t instanceof ge(t).HTMLElement||t instanceof HTMLElement}function ve(t){return"undefined"!=typeof ShadowRoot&&(t instanceof ge(t).ShadowRoot||t instanceof ShadowRoot)}const ye={name:"applyStyles",enabled:!0,phase:"write",fn:function(t){var e=t.state;Object.keys(e.elements).forEach(function(t){var i=e.styles[t]||{},n=e.attributes[t]||{},s=e.elements[t];be(s)&&me(s)&&(Object.assign(s.style,i),Object.keys(n).forEach(function(t){var e=n[t];!1===e?s.removeAttribute(t):s.setAttribute(t,!0===e?"":e)}))})},effect:function(t){var e=t.state,i={popper:{position:e.options.strategy,left:"0",top:"0",margin:"0"},arrow:{position:"absolute"},reference:{}};return Object.assign(e.elements.popper.style,i.popper),e.styles=i,e.elements.arrow&&Object.assign(e.elements.arrow.style,i.arrow),function(){Object.keys(e.elements).forEach(function(t){var n=e.elements[t],s=e.attributes[t]||{},o=Object.keys(e.styles.hasOwnProperty(t)?e.styles[t]:i[t]).reduce(function(t,e){return t[e]="",t},{});be(n)&&me(n)&&(Object.assign(n.style,o),Object.keys(s).forEach(function(t){n.removeAttribute(t)}))})}},requires:["computeStyles"]};function we(t){return t.split("-")[0]}var Ae=Math.max,Ee=Math.min,Te=Math.round;function Ce(){var t=navigator.userAgentData;return null!=t&&t.brands&&Array.isArray(t.brands)?t.brands.map(function(t){return t.brand+"/"+t.version}).join(" "):navigator.userAgent}function Oe(){return!/^((?!chrome|android).)*safari/i.test(Ce())}function xe(t,e,i){void 0===e&&(e=!1),void 0===i&&(i=!1);var n=t.getBoundingClientRect(),s=1,o=1;e&&be(t)&&(s=t.offsetWidth>0&&Te(n.width)/t.offsetWidth||1,o=t.offsetHeight>0&&Te(n.height)/t.offsetHeight||1);var r=(_e(t)?ge(t):window).visualViewport,a=!Oe()&&i,l=(n.left+(a&&r?r.offsetLeft:0))/s,c=(n.top+(a&&r?r.offsetTop:0))/o,h=n.width/s,d=n.height/o;return{width:h,height:d,top:c,right:l+h,bottom:c+d,left:l,x:l,y:c}}function ke(t){var e=xe(t),i=t.offsetWidth,n=t.offsetHeight;return Math.abs(e.width-i)<=1&&(i=e.width),Math.abs(e.height-n)<=1&&(n=e.height),{x:t.offsetLeft,y:t.offsetTop,width:i,height:n}}function Le(t,e){var i=e.getRootNode&&e.getRootNode();if(t.contains(e))return!0;if(i&&ve(i)){var n=e;do{if(n&&t.isSameNode(n))return!0;n=n.parentNode||n.host}while(n)}return!1}function Se(t){return ge(t).getComputedStyle(t)}function De(t){return["table","td","th"].indexOf(me(t))>=0}function $e(t){return((_e(t)?t.ownerDocument:t.document)||window.document).documentElement}function Ie(t){return"html"===me(t)?t:t.assignedSlot||t.parentNode||(ve(t)?t.host:null)||$e(t)}function Ne(t){return be(t)&&"fixed"!==Se(t).position?t.offsetParent:null}function Pe(t){for(var e=ge(t),i=Ne(t);i&&De(i)&&"static"===Se(i).position;)i=Ne(i);return i&&("html"===me(i)||"body"===me(i)&&"static"===Se(i).position)?e:i||function(t){var e=/firefox/i.test(Ce());if(/Trident/i.test(Ce())&&be(t)&&"fixed"===Se(t).position)return null;var i=Ie(t);for(ve(i)&&(i=i.host);be(i)&&["html","body"].indexOf(me(i))<0;){var n=Se(i);if("none"!==n.transform||"none"!==n.perspective||"paint"===n.contain||-1!==["transform","perspective"].indexOf(n.willChange)||e&&"filter"===n.willChange||e&&n.filter&&"none"!==n.filter)return i;i=i.parentNode}return null}(t)||e}function je(t){return["top","bottom"].indexOf(t)>=0?"x":"y"}function Me(t,e,i){return Ae(t,Ee(e,i))}function Fe(t){return Object.assign({},{top:0,right:0,bottom:0,left:0},t)}function He(t,e){return e.reduce(function(e,i){return e[i]=t,e},{})}const We={name:"arrow",enabled:!0,phase:"main",fn:function(t){var e,i=t.state,n=t.name,s=t.options,o=i.elements.arrow,r=i.modifiersData.popperOffsets,a=we(i.placement),l=je(a),c=[Xt,Qt].indexOf(a)>=0?"height":"width";if(o&&r){var h=function(t,e){return Fe("number"!=typeof(t="function"==typeof t?t(Object.assign({},e.rects,{placement:e.placement})):t)?t:He(t,Ut))}(s.padding,i),d=ke(o),u="y"===l?Vt:Xt,f="y"===l?Kt:Qt,p=i.rects.reference[c]+i.rects.reference[l]-r[l]-i.rects.popper[c],m=r[l]-i.rects.reference[l],g=Pe(o),_=g?"y"===l?g.clientHeight||0:g.clientWidth||0:0,b=p/2-m/2,v=h[u],y=_-d[c]-h[f],w=_/2-d[c]/2+b,A=Me(v,w,y),E=l;i.modifiersData[n]=((e={})[E]=A,e.centerOffset=A-w,e)}},effect:function(t){var e=t.state,i=t.options.element,n=void 0===i?"[data-popper-arrow]":i;null!=n&&("string"!=typeof n||(n=e.elements.popper.querySelector(n)))&&Le(e.elements.popper,n)&&(e.elements.arrow=n)},requires:["popperOffsets"],requiresIfExists:["preventOverflow"]};function Be(t){return t.split("-")[1]}var ze={top:"auto",right:"auto",bottom:"auto",left:"auto"};function Re(t){var e,i=t.popper,n=t.popperRect,s=t.placement,o=t.variation,r=t.offsets,a=t.position,l=t.gpuAcceleration,c=t.adaptive,h=t.roundOffsets,d=t.isFixed,u=r.x,f=void 0===u?0:u,p=r.y,m=void 0===p?0:p,g="function"==typeof h?h({x:f,y:m}):{x:f,y:m};f=g.x,m=g.y;var _=r.hasOwnProperty("x"),b=r.hasOwnProperty("y"),v=Xt,y=Vt,w=window;if(c){var A=Pe(i),E="clientHeight",T="clientWidth";A===ge(i)&&"static"!==Se(A=$e(i)).position&&"absolute"===a&&(E="scrollHeight",T="scrollWidth"),(s===Vt||(s===Xt||s===Qt)&&o===Jt)&&(y=Kt,m-=(d&&A===w&&w.visualViewport?w.visualViewport.height:A[E])-n.height,m*=l?1:-1),s!==Xt&&(s!==Vt&&s!==Kt||o!==Jt)||(v=Qt,f-=(d&&A===w&&w.visualViewport?w.visualViewport.width:A[T])-n.width,f*=l?1:-1)}var C,O=Object.assign({position:a},c&&ze),x=!0===h?function(t,e){var i=t.x,n=t.y,s=e.devicePixelRatio||1;return{x:Te(i*s)/s||0,y:Te(n*s)/s||0}}({x:f,y:m},ge(i)):{x:f,y:m};return f=x.x,m=x.y,l?Object.assign({},O,((C={})[y]=b?"0":"",C[v]=_?"0":"",C.transform=(w.devicePixelRatio||1)<=1?"translate("+f+"px, "+m+"px)":"translate3d("+f+"px, "+m+"px, 0)",C)):Object.assign({},O,((e={})[y]=b?m+"px":"",e[v]=_?f+"px":"",e.transform="",e))}const qe={name:"computeStyles",enabled:!0,phase:"beforeWrite",fn:function(t){var e=t.state,i=t.options,n=i.gpuAcceleration,s=void 0===n||n,o=i.adaptive,r=void 0===o||o,a=i.roundOffsets,l=void 0===a||a,c={placement:we(e.placement),variation:Be(e.placement),popper:e.elements.popper,popperRect:e.rects.popper,gpuAcceleration:s,isFixed:"fixed"===e.options.strategy};null!=e.modifiersData.popperOffsets&&(e.styles.popper=Object.assign({},e.styles.popper,Re(Object.assign({},c,{offsets:e.modifiersData.popperOffsets,position:e.options.strategy,adaptive:r,roundOffsets:l})))),null!=e.modifiersData.arrow&&(e.styles.arrow=Object.assign({},e.styles.arrow,Re(Object.assign({},c,{offsets:e.modifiersData.arrow,position:"absolute",adaptive:!1,roundOffsets:l})))),e.attributes.popper=Object.assign({},e.attributes.popper,{"data-popper-placement":e.placement})},data:{}};var Ve={passive:!0};const Ke={name:"eventListeners",enabled:!0,phase:"write",fn:function(){},effect:function(t){var e=t.state,i=t.instance,n=t.options,s=n.scroll,o=void 0===s||s,r=n.resize,a=void 0===r||r,l=ge(e.elements.popper),c=[].concat(e.scrollParents.reference,e.scrollParents.popper);return o&&c.forEach(function(t){t.addEventListener("scroll",i.update,Ve)}),a&&l.addEventListener("resize",i.update,Ve),function(){o&&c.forEach(function(t){t.removeEventListener("scroll",i.update,Ve)}),a&&l.removeEventListener("resize",i.update,Ve)}},data:{}};var Qe={left:"right",right:"left",bottom:"top",top:"bottom"};function Xe(t){return t.replace(/left|right|bottom|top/g,function(t){return Qe[t]})}var Ye={start:"end",end:"start"};function Ue(t){return t.replace(/start|end/g,function(t){return Ye[t]})}function Ge(t){var e=ge(t);return{scrollLeft:e.pageXOffset,scrollTop:e.pageYOffset}}function Je(t){return xe($e(t)).left+Ge(t).scrollLeft}function Ze(t){var e=Se(t),i=e.overflow,n=e.overflowX,s=e.overflowY;return/auto|scroll|overlay|hidden/.test(i+s+n)}function ti(t){return["html","body","#document"].indexOf(me(t))>=0?t.ownerDocument.body:be(t)&&Ze(t)?t:ti(Ie(t))}function ei(t,e){var i;void 0===e&&(e=[]);var n=ti(t),s=n===(null==(i=t.ownerDocument)?void 0:i.body),o=ge(n),r=s?[o].concat(o.visualViewport||[],Ze(n)?n:[]):n,a=e.concat(r);return s?a:a.concat(ei(Ie(r)))}function ii(t){return Object.assign({},t,{left:t.x,top:t.y,right:t.x+t.width,bottom:t.y+t.height})}function ni(t,e,i){return e===te?ii(function(t,e){var i=ge(t),n=$e(t),s=i.visualViewport,o=n.clientWidth,r=n.clientHeight,a=0,l=0;if(s){o=s.width,r=s.height;var c=Oe();(c||!c&&"fixed"===e)&&(a=s.offsetLeft,l=s.offsetTop)}return{width:o,height:r,x:a+Je(t),y:l}}(t,i)):_e(e)?function(t,e){var i=xe(t,!1,"fixed"===e);return i.top=i.top+t.clientTop,i.left=i.left+t.clientLeft,i.bottom=i.top+t.clientHeight,i.right=i.left+t.clientWidth,i.width=t.clientWidth,i.height=t.clientHeight,i.x=i.left,i.y=i.top,i}(e,i):ii(function(t){var e,i=$e(t),n=Ge(t),s=null==(e=t.ownerDocument)?void 0:e.body,o=Ae(i.scrollWidth,i.clientWidth,s?s.scrollWidth:0,s?s.clientWidth:0),r=Ae(i.scrollHeight,i.clientHeight,s?s.scrollHeight:0,s?s.clientHeight:0),a=-n.scrollLeft+Je(t),l=-n.scrollTop;return"rtl"===Se(s||i).direction&&(a+=Ae(i.clientWidth,s?s.clientWidth:0)-o),{width:o,height:r,x:a,y:l}}($e(t)))}function si(t){var e,i=t.reference,n=t.element,s=t.placement,o=s?we(s):null,r=s?Be(s):null,a=i.x+i.width/2-n.width/2,l=i.y+i.height/2-n.height/2;switch(o){case Vt:e={x:a,y:i.y-n.height};break;case Kt:e={x:a,y:i.y+i.height};break;case Qt:e={x:i.x+i.width,y:l};break;case Xt:e={x:i.x-n.width,y:l};break;default:e={x:i.x,y:i.y}}var c=o?je(o):null;if(null!=c){var h="y"===c?"height":"width";switch(r){case Gt:e[c]=e[c]-(i[h]/2-n[h]/2);break;case Jt:e[c]=e[c]+(i[h]/2-n[h]/2)}}return e}function oi(t,e){void 0===e&&(e={});var i=e,n=i.placement,s=void 0===n?t.placement:n,o=i.strategy,r=void 0===o?t.strategy:o,a=i.boundary,l=void 0===a?Zt:a,c=i.rootBoundary,h=void 0===c?te:c,d=i.elementContext,u=void 0===d?ee:d,f=i.altBoundary,p=void 0!==f&&f,m=i.padding,g=void 0===m?0:m,_=Fe("number"!=typeof g?g:He(g,Ut)),b=u===ee?ie:ee,v=t.rects.popper,y=t.elements[p?b:u],w=function(t,e,i,n){var s="clippingParents"===e?function(t){var e=ei(Ie(t)),i=["absolute","fixed"].indexOf(Se(t).position)>=0&&be(t)?Pe(t):t;return _e(i)?e.filter(function(t){return _e(t)&&Le(t,i)&&"body"!==me(t)}):[]}(t):[].concat(e),o=[].concat(s,[i]),r=o[0],a=o.reduce(function(e,i){var s=ni(t,i,n);return e.top=Ae(s.top,e.top),e.right=Ee(s.right,e.right),e.bottom=Ee(s.bottom,e.bottom),e.left=Ae(s.left,e.left),e},ni(t,r,n));return a.width=a.right-a.left,a.height=a.bottom-a.top,a.x=a.left,a.y=a.top,a}(_e(y)?y:y.contextElement||$e(t.elements.popper),l,h,r),A=xe(t.elements.reference),E=si({reference:A,element:v,placement:s}),T=ii(Object.assign({},v,E)),C=u===ee?T:A,O={top:w.top-C.top+_.top,bottom:C.bottom-w.bottom+_.bottom,left:w.left-C.left+_.left,right:C.right-w.right+_.right},x=t.modifiersData.offset;if(u===ee&&x){var k=x[s];Object.keys(O).forEach(function(t){var e=[Qt,Kt].indexOf(t)>=0?1:-1,i=[Vt,Kt].indexOf(t)>=0?"y":"x";O[t]+=k[i]*e})}return O}function ri(t,e){void 0===e&&(e={});var i=e,n=i.placement,s=i.boundary,o=i.rootBoundary,r=i.padding,a=i.flipVariations,l=i.allowedAutoPlacements,c=void 0===l?se:l,h=Be(n),d=h?a?ne:ne.filter(function(t){return Be(t)===h}):Ut,u=d.filter(function(t){return c.indexOf(t)>=0});0===u.length&&(u=d);var f=u.reduce(function(e,i){return e[i]=oi(t,{placement:i,boundary:s,rootBoundary:o,padding:r})[we(i)],e},{});return Object.keys(f).sort(function(t,e){return f[t]-f[e]})}const ai={name:"flip",enabled:!0,phase:"main",fn:function(t){var e=t.state,i=t.options,n=t.name;if(!e.modifiersData[n]._skip){for(var s=i.mainAxis,o=void 0===s||s,r=i.altAxis,a=void 0===r||r,l=i.fallbackPlacements,c=i.padding,h=i.boundary,d=i.rootBoundary,u=i.altBoundary,f=i.flipVariations,p=void 0===f||f,m=i.allowedAutoPlacements,g=e.options.placement,_=we(g),b=l||(_!==g&&p?function(t){if(we(t)===Yt)return[];var e=Xe(t);return[Ue(t),e,Ue(e)]}(g):[Xe(g)]),v=[g].concat(b).reduce(function(t,i){return t.concat(we(i)===Yt?ri(e,{placement:i,boundary:h,rootBoundary:d,padding:c,flipVariations:p,allowedAutoPlacements:m}):i)},[]),y=e.rects.reference,w=e.rects.popper,A=new Map,E=!0,T=v[0],C=0;C<v.length;C++){var O=v[C],x=we(O),k=Be(O)===Gt,L=[Vt,Kt].indexOf(x)>=0,S=L?"width":"height",D=oi(e,{placement:O,boundary:h,rootBoundary:d,altBoundary:u,padding:c}),$=L?k?Qt:Xt:k?Kt:Vt;y[S]>w[S]&&($=Xe($));var I=Xe($),N=[];if(o&&N.push(D[x]<=0),a&&N.push(D[$]<=0,D[I]<=0),N.every(function(t){return t})){T=O,E=!1;break}A.set(O,N)}if(E)for(var P=function(t){var e=v.find(function(e){var i=A.get(e);if(i)return i.slice(0,t).every(function(t){return t})});if(e)return T=e,"break"},j=p?3:1;j>0&&"break"!==P(j);j--);e.placement!==T&&(e.modifiersData[n]._skip=!0,e.placement=T,e.reset=!0)}},requiresIfExists:["offset"],data:{_skip:!1}};function li(t,e,i){return void 0===i&&(i={x:0,y:0}),{top:t.top-e.height-i.y,right:t.right-e.width+i.x,bottom:t.bottom-e.height+i.y,left:t.left-e.width-i.x}}function ci(t){return[Vt,Qt,Kt,Xt].some(function(e){return t[e]>=0})}const hi={name:"hide",enabled:!0,phase:"main",requiresIfExists:["preventOverflow"],fn:function(t){var e=t.state,i=t.name,n=e.rects.reference,s=e.rects.popper,o=e.modifiersData.preventOverflow,r=oi(e,{elementContext:"reference"}),a=oi(e,{altBoundary:!0}),l=li(r,n),c=li(a,s,o),h=ci(l),d=ci(c);e.modifiersData[i]={referenceClippingOffsets:l,popperEscapeOffsets:c,isReferenceHidden:h,hasPopperEscaped:d},e.attributes.popper=Object.assign({},e.attributes.popper,{"data-popper-reference-hidden":h,"data-popper-escaped":d})}},di={name:"offset",enabled:!0,phase:"main",requires:["popperOffsets"],fn:function(t){var e=t.state,i=t.options,n=t.name,s=i.offset,o=void 0===s?[0,0]:s,r=se.reduce(function(t,i){return t[i]=function(t,e,i){var n=we(t),s=[Xt,Vt].indexOf(n)>=0?-1:1,o="function"==typeof i?i(Object.assign({},e,{placement:t})):i,r=o[0],a=o[1];return r=r||0,a=(a||0)*s,[Xt,Qt].indexOf(n)>=0?{x:a,y:r}:{x:r,y:a}}(i,e.rects,o),t},{}),a=r[e.placement],l=a.x,c=a.y;null!=e.modifiersData.popperOffsets&&(e.modifiersData.popperOffsets.x+=l,e.modifiersData.popperOffsets.y+=c),e.modifiersData[n]=r}},ui={name:"popperOffsets",enabled:!0,phase:"read",fn:function(t){var e=t.state,i=t.name;e.modifiersData[i]=si({reference:e.rects.reference,element:e.rects.popper,placement:e.placement})},data:{}},fi={name:"preventOverflow",enabled:!0,phase:"main",fn:function(t){var e=t.state,i=t.options,n=t.name,s=i.mainAxis,o=void 0===s||s,r=i.altAxis,a=void 0!==r&&r,l=i.boundary,c=i.rootBoundary,h=i.altBoundary,d=i.padding,u=i.tether,f=void 0===u||u,p=i.tetherOffset,m=void 0===p?0:p,g=oi(e,{boundary:l,rootBoundary:c,padding:d,altBoundary:h}),_=we(e.placement),b=Be(e.placement),v=!b,y=je(_),w="x"===y?"y":"x",A=e.modifiersData.popperOffsets,E=e.rects.reference,T=e.rects.popper,C="function"==typeof m?m(Object.assign({},e.rects,{placement:e.placement})):m,O="number"==typeof C?{mainAxis:C,altAxis:C}:Object.assign({mainAxis:0,altAxis:0},C),x=e.modifiersData.offset?e.modifiersData.offset[e.placement]:null,k={x:0,y:0};if(A){if(o){var L,S="y"===y?Vt:Xt,D="y"===y?Kt:Qt,$="y"===y?"height":"width",I=A[y],N=I+g[S],P=I-g[D],j=f?-T[$]/2:0,M=b===Gt?E[$]:T[$],F=b===Gt?-T[$]:-E[$],H=e.elements.arrow,W=f&&H?ke(H):{width:0,height:0},B=e.modifiersData["arrow#persistent"]?e.modifiersData["arrow#persistent"].padding:{top:0,right:0,bottom:0,left:0},z=B[S],R=B[D],q=Me(0,E[$],W[$]),V=v?E[$]/2-j-q-z-O.mainAxis:M-q-z-O.mainAxis,K=v?-E[$]/2+j+q+R+O.mainAxis:F+q+R+O.mainAxis,Q=e.elements.arrow&&Pe(e.elements.arrow),X=Q?"y"===y?Q.clientTop||0:Q.clientLeft||0:0,Y=null!=(L=null==x?void 0:x[y])?L:0,U=I+K-Y,G=Me(f?Ee(N,I+V-Y-X):N,I,f?Ae(P,U):P);A[y]=G,k[y]=G-I}if(a){var J,Z="x"===y?Vt:Xt,tt="x"===y?Kt:Qt,et=A[w],it="y"===w?"height":"width",nt=et+g[Z],st=et-g[tt],ot=-1!==[Vt,Xt].indexOf(_),rt=null!=(J=null==x?void 0:x[w])?J:0,at=ot?nt:et-E[it]-T[it]-rt+O.altAxis,lt=ot?et+E[it]+T[it]-rt-O.altAxis:st,ct=f&&ot?function(t,e,i){var n=Me(t,e,i);return n>i?i:n}(at,et,lt):Me(f?at:nt,et,f?lt:st);A[w]=ct,k[w]=ct-et}e.modifiersData[n]=k}},requiresIfExists:["offset"]};function pi(t,e,i){void 0===i&&(i=!1);var n,s,o=be(e),r=be(e)&&function(t){var e=t.getBoundingClientRect(),i=Te(e.width)/t.offsetWidth||1,n=Te(e.height)/t.offsetHeight||1;return 1!==i||1!==n}(e),a=$e(e),l=xe(t,r,i),c={scrollLeft:0,scrollTop:0},h={x:0,y:0};return(o||!o&&!i)&&(("body"!==me(e)||Ze(a))&&(c=(n=e)!==ge(n)&&be(n)?{scrollLeft:(s=n).scrollLeft,scrollTop:s.scrollTop}:Ge(n)),be(e)?((h=xe(e,!0)).x+=e.clientLeft,h.y+=e.clientTop):a&&(h.x=Je(a))),{x:l.left+c.scrollLeft-h.x,y:l.top+c.scrollTop-h.y,width:l.width,height:l.height}}function mi(t){var e=new Map,i=new Set,n=[];function s(t){i.add(t.name),[].concat(t.requires||[],t.requiresIfExists||[]).forEach(function(t){if(!i.has(t)){var n=e.get(t);n&&s(n)}}),n.push(t)}return t.forEach(function(t){e.set(t.name,t)}),t.forEach(function(t){i.has(t.name)||s(t)}),n}var gi={placement:"bottom",modifiers:[],strategy:"absolute"};function _i(){for(var t=arguments.length,e=new Array(t),i=0;i<t;i++)e[i]=arguments[i];return!e.some(function(t){return!(t&&"function"==typeof t.getBoundingClientRect)})}function bi(t){void 0===t&&(t={});var e=t,i=e.defaultModifiers,n=void 0===i?[]:i,s=e.defaultOptions,o=void 0===s?gi:s;return function(t,e,i){void 0===i&&(i=o);var s,r,a={placement:"bottom",orderedModifiers:[],options:Object.assign({},gi,o),modifiersData:{},elements:{reference:t,popper:e},attributes:{},styles:{}},l=[],c=!1,h={state:a,setOptions:function(i){var s="function"==typeof i?i(a.options):i;d(),a.options=Object.assign({},o,a.options,s),a.scrollParents={reference:_e(t)?ei(t):t.contextElement?ei(t.contextElement):[],popper:ei(e)};var r,c,u=function(t){var e=mi(t);return pe.reduce(function(t,i){return t.concat(e.filter(function(t){return t.phase===i}))},[])}((r=[].concat(n,a.options.modifiers),c=r.reduce(function(t,e){var i=t[e.name];return t[e.name]=i?Object.assign({},i,e,{options:Object.assign({},i.options,e.options),data:Object.assign({},i.data,e.data)}):e,t},{}),Object.keys(c).map(function(t){return c[t]})));return a.orderedModifiers=u.filter(function(t){return t.enabled}),a.orderedModifiers.forEach(function(t){var e=t.name,i=t.options,n=void 0===i?{}:i,s=t.effect;if("function"==typeof s){var o=s({state:a,name:e,instance:h,options:n});l.push(o||function(){})}}),h.update()},forceUpdate:function(){if(!c){var t=a.elements,e=t.reference,i=t.popper;if(_i(e,i)){a.rects={reference:pi(e,Pe(i),"fixed"===a.options.strategy),popper:ke(i)},a.reset=!1,a.placement=a.options.placement,a.orderedModifiers.forEach(function(t){return a.modifiersData[t.name]=Object.assign({},t.data)});for(var n=0;n<a.orderedModifiers.length;n++)if(!0!==a.reset){var s=a.orderedModifiers[n],o=s.fn,r=s.options,l=void 0===r?{}:r,d=s.name;"function"==typeof o&&(a=o({state:a,options:l,name:d,instance:h})||a)}else a.reset=!1,n=-1}}},update:(s=function(){return new Promise(function(t){h.forceUpdate(),t(a)})},function(){return r||(r=new Promise(function(t){Promise.resolve().then(function(){r=void 0,t(s())})})),r}),destroy:function(){d(),c=!0}};if(!_i(t,e))return h;function d(){l.forEach(function(t){return t()}),l=[]}return h.setOptions(i).then(function(t){!c&&i.onFirstUpdate&&i.onFirstUpdate(t)}),h}}var vi=bi(),yi=bi({defaultModifiers:[Ke,ui,qe,ye]}),wi=bi({defaultModifiers:[Ke,ui,qe,ye,di,ai,fi,We,hi]});const Ai=Object.freeze(Object.defineProperty({__proto__:null,afterMain:he,afterRead:ae,afterWrite:fe,applyStyles:ye,arrow:We,auto:Yt,basePlacements:Ut,beforeMain:le,beforeRead:oe,beforeWrite:de,bottom:Kt,clippingParents:Zt,computeStyles:qe,createPopper:wi,createPopperBase:vi,createPopperLite:yi,detectOverflow:oi,end:Jt,eventListeners:Ke,flip:ai,hide:hi,left:Xt,main:ce,modifierPhases:pe,offset:di,placements:se,popper:ee,popperGenerator:bi,popperOffsets:ui,preventOverflow:fi,read:re,reference:ie,right:Qt,start:Gt,top:Vt,variationPlacements:ne,viewport:te,write:ue},Symbol.toStringTag,{value:"Module"})),Ei="dropdown",Ti=".bs.dropdown",Ci=".data-api",Oi="ArrowUp",xi="ArrowDown",ki=`hide${Ti}`,Li=`hidden${Ti}`,Si=`show${Ti}`,Di=`shown${Ti}`,$i=`click${Ti}${Ci}`,Ii=`keydown${Ti}${Ci}`,Ni=`keyup${Ti}${Ci}`,Pi="show",ji='[data-bs-toggle="dropdown"]:not(.disabled):not(:disabled)',Mi=`${ji}.${Pi}`,Fi=".dropdown-menu",Hi=m()?"top-end":"top-start",Wi=m()?"top-start":"top-end",Bi=m()?"bottom-end":"bottom-start",zi=m()?"bottom-start":"bottom-end",Ri=m()?"left-start":"right-start",qi=m()?"right-start":"left-start",Vi={autoClose:!0,boundary:"clippingParents",display:"dynamic",offset:[0,2],popperConfig:null,reference:"toggle"},Ki={autoClose:"(boolean|string)",boundary:"(string|element)",display:"string",offset:"(array|string|function)",popperConfig:"(null|object|function)",reference:"(string|element|object)"};class Qi extends B{constructor(t,e){super(t,e),this._popper=null,this._parent=this._element.parentNode,this._menu=R.next(this._element,Fi)[0]||R.prev(this._element,Fi)[0]||R.findOne(Fi,this._parent),this._inNavbar=this._detectNavbar()}static get Default(){return Vi}static get DefaultType(){return Ki}static get NAME(){return Ei}toggle(){return this._isShown()?this.hide():this.show()}show(){if(c(this._element)||this._isShown())return;const t={relatedTarget:this._element};if(!P.trigger(this._element,Si,t).defaultPrevented){if(this._createPopper(),"ontouchstart"in document.documentElement&&!this._parent.closest(".navbar-nav"))for(const t of[].concat(...document.body.children))P.on(t,"mouseover",d);this._element.focus(),this._element.setAttribute("aria-expanded",!0),this._menu.classList.add(Pi),this._element.classList.add(Pi),P.trigger(this._element,Di,t)}}hide(){if(c(this._element)||!this._isShown())return;const t={relatedTarget:this._element};this._completeHide(t)}dispose(){this._popper&&this._popper.destroy(),super.dispose()}update(){this._inNavbar=this._detectNavbar(),this._popper&&this._popper.update()}_completeHide(t){if(!P.trigger(this._element,ki,t).defaultPrevented){if("ontouchstart"in document.documentElement)for(const t of[].concat(...document.body.children))P.off(t,"mouseover",d);this._popper&&this._popper.destroy(),this._menu.classList.remove(Pi),this._element.classList.remove(Pi),this._element.setAttribute("aria-expanded","false"),H.removeDataAttribute(this._menu,"popper"),P.trigger(this._element,Li,t)}}_getConfig(t){if("object"==typeof(t=super._getConfig(t)).reference&&!r(t.reference)&&"function"!=typeof t.reference.getBoundingClientRect)throw new TypeError(`${Ei.toUpperCase()}: Option "reference" provided type "object" without a required "getBoundingClientRect" method.`);return t}_createPopper(){if(void 0===Ai)throw new TypeError("Bootstrap's dropdowns require Popper (https://popper.js.org/docs/v2/)");let t=this._element;"parent"===this._config.reference?t=this._parent:r(this._config.reference)?t=a(this._config.reference):"object"==typeof this._config.reference&&(t=this._config.reference);const e=this._getPopperConfig();this._popper=wi(t,this._menu,e)}_isShown(){return this._menu.classList.contains(Pi)}_getPlacement(){const t=this._parent;if(t.classList.contains("dropend"))return Ri;if(t.classList.contains("dropstart"))return qi;if(t.classList.contains("dropup-center"))return"top";if(t.classList.contains("dropdown-center"))return"bottom";const e="end"===getComputedStyle(this._menu).getPropertyValue("--bs-position").trim();return t.classList.contains("dropup")?e?Wi:Hi:e?zi:Bi}_detectNavbar(){return null!==this._element.closest(".navbar")}_getOffset(){const{offset:t}=this._config;return"string"==typeof t?t.split(",").map(t=>Number.parseInt(t,10)):"function"==typeof t?e=>t(e,this._element):t}_getPopperConfig(){const t={placement:this._getPlacement(),modifiers:[{name:"preventOverflow",options:{boundary:this._config.boundary}},{name:"offset",options:{offset:this._getOffset()}}]};return(this._inNavbar||"static"===this._config.display)&&(H.setDataAttribute(this._menu,"popper","static"),t.modifiers=[{name:"applyStyles",enabled:!1}]),{...t,..._(this._config.popperConfig,[void 0,t])}}_selectMenuItem({key:t,target:e}){const i=R.find(".dropdown-menu .dropdown-item:not(.disabled):not(:disabled)",this._menu).filter(t=>l(t));i.length&&v(i,e,t===xi,!i.includes(e)).focus()}static jQueryInterface(t){return this.each(function(){const e=Qi.getOrCreateInstance(this,t);if("string"==typeof t){if(void 0===e[t])throw new TypeError(`No method named "${t}"`);e[t]()}})}static clearMenus(t){if(2===t.button||"keyup"===t.type&&"Tab"!==t.key)return;const e=R.find(Mi);for(const i of e){const e=Qi.getInstance(i);if(!e||!1===e._config.autoClose)continue;const n=t.composedPath(),s=n.includes(e._menu);if(n.includes(e._element)||"inside"===e._config.autoClose&&!s||"outside"===e._config.autoClose&&s)continue;if(e._menu.contains(t.target)&&("keyup"===t.type&&"Tab"===t.key||/input|select|option|textarea|form/i.test(t.target.tagName)))continue;const o={relatedTarget:e._element};"click"===t.type&&(o.clickEvent=t),e._completeHide(o)}}static dataApiKeydownHandler(t){const e=/input|textarea/i.test(t.target.tagName),i="Escape"===t.key,n=[Oi,xi].includes(t.key);if(!n&&!i)return;if(e&&!i)return;t.preventDefault();const s=this.matches(ji)?this:R.prev(this,ji)[0]||R.next(this,ji)[0]||R.findOne(ji,t.delegateTarget.parentNode),o=Qi.getOrCreateInstance(s);if(n)return t.stopPropagation(),o.show(),void o._selectMenuItem(t);o._isShown()&&(t.stopPropagation(),o.hide(),s.focus())}}P.on(document,Ii,ji,Qi.dataApiKeydownHandler),P.on(document,Ii,Fi,Qi.dataApiKeydownHandler),P.on(document,$i,Qi.clearMenus),P.on(document,Ni,Qi.clearMenus),P.on(document,$i,ji,function(t){t.preventDefault(),Qi.getOrCreateInstance(this).toggle()}),g(Qi);const Xi="backdrop",Yi="show",Ui=`mousedown.bs.${Xi}`,Gi={className:"modal-backdrop",clickCallback:null,isAnimated:!1,isVisible:!0,rootElement:"body"},Ji={className:"string",clickCallback:"(function|null)",isAnimated:"boolean",isVisible:"boolean",rootElement:"(element|string)"};class Zi extends W{constructor(t){super(),this._config=this._getConfig(t),this._isAppended=!1,this._element=null}static get Default(){return Gi}static get DefaultType(){return Ji}static get NAME(){return Xi}show(t){if(!this._config.isVisible)return void _(t);this._append();const e=this._getElement();this._config.isAnimated&&u(e),e.classList.add(Yi),this._emulateAnimation(()=>{_(t)})}hide(t){this._config.isVisible?(this._getElement().classList.remove(Yi),this._emulateAnimation(()=>{this.dispose(),_(t)})):_(t)}dispose(){this._isAppended&&(P.off(this._element,Ui),this._element.remove(),this._isAppended=!1)}_getElement(){if(!this._element){const t=document.createElement("div");t.className=this._config.className,this._config.isAnimated&&t.classList.add("fade"),this._element=t}return this._element}_configAfterMerge(t){return t.rootElement=a(t.rootElement),t}_append(){if(this._isAppended)return;const t=this._getElement();this._config.rootElement.append(t),P.on(t,Ui,()=>{_(this._config.clickCallback)}),this._isAppended=!0}_emulateAnimation(t){b(t,this._getElement(),this._config.isAnimated)}}const tn=".bs.focustrap",en=`focusin${tn}`,nn=`keydown.tab${tn}`,sn="backward",on={autofocus:!0,trapElement:null},rn={autofocus:"boolean",trapElement:"element"};class an extends W{constructor(t){super(),this._config=this._getConfig(t),this._isActive=!1,this._lastTabNavDirection=null}static get Default(){return on}static get DefaultType(){return rn}static get NAME(){return"focustrap"}activate(){this._isActive||(this._config.autofocus&&this._config.trapElement.focus(),P.off(document,tn),P.on(document,en,t=>this._handleFocusin(t)),P.on(document,nn,t=>this._handleKeydown(t)),this._isActive=!0)}deactivate(){this._isActive&&(this._isActive=!1,P.off(document,tn))}_handleFocusin(t){const{trapElement:e}=this._config;if(t.target===document||t.target===e||e.contains(t.target))return;const i=R.focusableChildren(e);0===i.length?e.focus():this._lastTabNavDirection===sn?i[i.length-1].focus():i[0].focus()}_handleKeydown(t){"Tab"===t.key&&(this._lastTabNavDirection=t.shiftKey?sn:"forward")}}const ln=".fixed-top, .fixed-bottom, .is-fixed, .sticky-top",cn=".sticky-top",hn="padding-right",dn="margin-right";class un{constructor(){this._element=document.body}getWidth(){const t=document.documentElement.clientWidth;return Math.abs(window.innerWidth-t)}hide(){const t=this.getWidth();this._disableOverFlow(),this._setElementAttributes(this._element,hn,e=>e+t),this._setElementAttributes(ln,hn,e=>e+t),this._setElementAttributes(cn,dn,e=>e-t)}reset(){this._resetElementAttributes(this._element,"overflow"),this._resetElementAttributes(this._element,hn),this._resetElementAttributes(ln,hn),this._resetElementAttributes(cn,dn)}isOverflowing(){return this.getWidth()>0}_disableOverFlow(){this._saveInitialAttribute(this._element,"overflow"),this._element.style.overflow="hidden"}_setElementAttributes(t,e,i){const n=this.getWidth();this._applyManipulationCallback(t,t=>{if(t!==this._element&&window.innerWidth>t.clientWidth+n)return;this._saveInitialAttribute(t,e);const s=window.getComputedStyle(t).getPropertyValue(e);t.style.setProperty(e,`${i(Number.parseFloat(s))}px`)})}_saveInitialAttribute(t,e){const i=t.style.getPropertyValue(e);i&&H.setDataAttribute(t,e,i)}_resetElementAttributes(t,e){this._applyManipulationCallback(t,t=>{const i=H.getDataAttribute(t,e);null!==i?(H.removeDataAttribute(t,e),t.style.setProperty(e,i)):t.style.removeProperty(e)})}_applyManipulationCallback(t,e){if(r(t))e(t);else for(const i of R.find(t,this._element))e(i)}}const fn=".bs.modal",pn=`hide${fn}`,mn=`hidePrevented${fn}`,gn=`hidden${fn}`,_n=`show${fn}`,bn=`shown${fn}`,vn=`resize${fn}`,yn=`click.dismiss${fn}`,wn=`mousedown.dismiss${fn}`,An=`keydown.dismiss${fn}`,En=`click${fn}.data-api`,Tn="modal-open",Cn="show",On="modal-static",xn={backdrop:!0,focus:!0,keyboard:!0},kn={backdrop:"(boolean|string)",focus:"boolean",keyboard:"boolean"};class Ln extends B{constructor(t,e){super(t,e),this._dialog=R.findOne(".modal-dialog",this._element),this._backdrop=this._initializeBackDrop(),this._focustrap=this._initializeFocusTrap(),this._isShown=!1,this._isTransitioning=!1,this._scrollBar=new un,this._addEventListeners()}static get Default(){return xn}static get DefaultType(){return kn}static get NAME(){return"modal"}toggle(t){return this._isShown?this.hide():this.show(t)}show(t){this._isShown||this._isTransitioning||P.trigger(this._element,_n,{relatedTarget:t}).defaultPrevented||(this._isShown=!0,this._isTransitioning=!0,this._scrollBar.hide(),document.body.classList.add(Tn),this._adjustDialog(),this._backdrop.show(()=>this._showElement(t)))}hide(){this._isShown&&!this._isTransitioning&&(P.trigger(this._element,pn).defaultPrevented||(this._isShown=!1,this._isTransitioning=!0,this._focustrap.deactivate(),this._element.classList.remove(Cn),this._queueCallback(()=>this._hideModal(),this._element,this._isAnimated())))}dispose(){P.off(window,fn),P.off(this._dialog,fn),this._backdrop.dispose(),this._focustrap.deactivate(),super.dispose()}handleUpdate(){this._adjustDialog()}_initializeBackDrop(){return new Zi({isVisible:Boolean(this._config.backdrop),isAnimated:this._isAnimated()})}_initializeFocusTrap(){return new an({trapElement:this._element})}_showElement(t){document.body.contains(this._element)||document.body.append(this._element),this._element.style.display="block",this._element.removeAttribute("aria-hidden"),this._element.setAttribute("aria-modal",!0),this._element.setAttribute("role","dialog"),this._element.scrollTop=0;const e=R.findOne(".modal-body",this._dialog);e&&(e.scrollTop=0),u(this._element),this._element.classList.add(Cn),this._queueCallback(()=>{this._config.focus&&this._focustrap.activate(),this._isTransitioning=!1,P.trigger(this._element,bn,{relatedTarget:t})},this._dialog,this._isAnimated())}_addEventListeners(){P.on(this._element,An,t=>{"Escape"===t.key&&(this._config.keyboard?this.hide():this._triggerBackdropTransition())}),P.on(window,vn,()=>{this._isShown&&!this._isTransitioning&&this._adjustDialog()}),P.on(this._element,wn,t=>{P.one(this._element,yn,e=>{this._element===t.target&&this._element===e.target&&("static"!==this._config.backdrop?this._config.backdrop&&this.hide():this._triggerBackdropTransition())})})}_hideModal(){this._element.style.display="none",this._element.setAttribute("aria-hidden",!0),this._element.removeAttribute("aria-modal"),this._element.removeAttribute("role"),this._isTransitioning=!1,this._backdrop.hide(()=>{document.body.classList.remove(Tn),this._resetAdjustments(),this._scrollBar.reset(),P.trigger(this._element,gn)})}_isAnimated(){return this._element.classList.contains("fade")}_triggerBackdropTransition(){if(P.trigger(this._element,mn).defaultPrevented)return;const t=this._element.scrollHeight>document.documentElement.clientHeight,e=this._element.style.overflowY;"hidden"===e||this._element.classList.contains(On)||(t||(this._element.style.overflowY="hidden"),this._element.classList.add(On),this._queueCallback(()=>{this._element.classList.remove(On),this._queueCallback(()=>{this._element.style.overflowY=e},this._dialog)},this._dialog),this._element.focus())}_adjustDialog(){const t=this._element.scrollHeight>document.documentElement.clientHeight,e=this._scrollBar.getWidth(),i=e>0;if(i&&!t){const t=m()?"paddingLeft":"paddingRight";this._element.style[t]=`${e}px`}if(!i&&t){const t=m()?"paddingRight":"paddingLeft";this._element.style[t]=`${e}px`}}_resetAdjustments(){this._element.style.paddingLeft="",this._element.style.paddingRight=""}static jQueryInterface(t,e){return this.each(function(){const i=Ln.getOrCreateInstance(this,t);if("string"==typeof t){if(void 0===i[t])throw new TypeError(`No method named "${t}"`);i[t](e)}})}}P.on(document,En,'[data-bs-toggle="modal"]',function(t){const e=R.getElementFromSelector(this);["A","AREA"].includes(this.tagName)&&t.preventDefault(),P.one(e,_n,t=>{t.defaultPrevented||P.one(e,gn,()=>{l(this)&&this.focus()})});const i=R.findOne(".modal.show");i&&Ln.getInstance(i).hide(),Ln.getOrCreateInstance(e).toggle(this)}),q(Ln),g(Ln);const Sn=".bs.offcanvas",Dn=".data-api",$n=`load${Sn}${Dn}`,In="show",Nn="showing",Pn="hiding",jn=".offcanvas.show",Mn=`show${Sn}`,Fn=`shown${Sn}`,Hn=`hide${Sn}`,Wn=`hidePrevented${Sn}`,Bn=`hidden${Sn}`,zn=`resize${Sn}`,Rn=`click${Sn}${Dn}`,qn=`keydown.dismiss${Sn}`,Vn={backdrop:!0,keyboard:!0,scroll:!1},Kn={backdrop:"(boolean|string)",keyboard:"boolean",scroll:"boolean"};class Qn extends B{constructor(t,e){super(t,e),this._isShown=!1,this._backdrop=this._initializeBackDrop(),this._focustrap=this._initializeFocusTrap(),this._addEventListeners()}static get Default(){return Vn}static get DefaultType(){return Kn}static get NAME(){return"offcanvas"}toggle(t){return this._isShown?this.hide():this.show(t)}show(t){this._isShown||P.trigger(this._element,Mn,{relatedTarget:t}).defaultPrevented||(this._isShown=!0,this._backdrop.show(),this._config.scroll||(new un).hide(),this._element.setAttribute("aria-modal",!0),this._element.setAttribute("role","dialog"),this._element.classList.add(Nn),this._queueCallback(()=>{this._config.scroll&&!this._config.backdrop||this._focustrap.activate(),this._element.classList.add(In),this._element.classList.remove(Nn),P.trigger(this._element,Fn,{relatedTarget:t})},this._element,!0))}hide(){this._isShown&&(P.trigger(this._element,Hn).defaultPrevented||(this._focustrap.deactivate(),this._element.blur(),this._isShown=!1,this._element.classList.add(Pn),this._backdrop.hide(),this._queueCallback(()=>{this._element.classList.remove(In,Pn),this._element.removeAttribute("aria-modal"),this._element.removeAttribute("role"),this._config.scroll||(new un).reset(),P.trigger(this._element,Bn)},this._element,!0)))}dispose(){this._backdrop.dispose(),this._focustrap.deactivate(),super.dispose()}_initializeBackDrop(){const t=Boolean(this._config.backdrop);return new Zi({className:"offcanvas-backdrop",isVisible:t,isAnimated:!0,rootElement:this._element.parentNode,clickCallback:t?()=>{"static"!==this._config.backdrop?this.hide():P.trigger(this._element,Wn)}:null})}_initializeFocusTrap(){return new an({trapElement:this._element})}_addEventListeners(){P.on(this._element,qn,t=>{"Escape"===t.key&&(this._config.keyboard?this.hide():P.trigger(this._element,Wn))})}static jQueryInterface(t){return this.each(function(){const e=Qn.getOrCreateInstance(this,t);if("string"==typeof t){if(void 0===e[t]||t.startsWith("_")||"constructor"===t)throw new TypeError(`No method named "${t}"`);e[t](this)}})}}P.on(document,Rn,'[data-bs-toggle="offcanvas"]',function(t){const e=R.getElementFromSelector(this);if(["A","AREA"].includes(this.tagName)&&t.preventDefault(),c(this))return;P.one(e,Bn,()=>{l(this)&&this.focus()});const i=R.findOne(jn);i&&i!==e&&Qn.getInstance(i).hide(),Qn.getOrCreateInstance(e).toggle(this)}),P.on(window,$n,()=>{for(const t of R.find(jn))Qn.getOrCreateInstance(t).show()}),P.on(window,zn,()=>{for(const t of R.find("[aria-modal][class*=show][class*=offcanvas-]"))"fixed"!==getComputedStyle(t).position&&Qn.getOrCreateInstance(t).hide()}),q(Qn),g(Qn);const Xn={"*":["class","dir","id","lang","role",/^aria-[\w-]*$/i],a:["target","href","title","rel"],area:[],b:[],br:[],col:[],code:[],dd:[],div:[],dl:[],dt:[],em:[],hr:[],h1:[],h2:[],h3:[],h4:[],h5:[],h6:[],i:[],img:["src","srcset","alt","title","width","height"],li:[],ol:[],p:[],pre:[],s:[],small:[],span:[],sub:[],sup:[],strong:[],u:[],ul:[]},Yn=new Set(["background","cite","href","itemtype","longdesc","poster","src","xlink:href"]),Un=/^(?!javascript:)(?:[a-z0-9+.-]+:|[^&:/?#]*(?:[/?#]|$))/i,Gn=(t,e)=>{const i=t.nodeName.toLowerCase();return e.includes(i)?!Yn.has(i)||Boolean(Un.test(t.nodeValue)):e.filter(t=>t instanceof RegExp).some(t=>t.test(i))},Jn={allowList:Xn,content:{},extraClass:"",html:!1,sanitize:!0,sanitizeFn:null,template:"<div></div>"},Zn={allowList:"object",content:"object",extraClass:"(string|function)",html:"boolean",sanitize:"boolean",sanitizeFn:"(null|function)",template:"string"},ts={entry:"(string|element|function|null)",selector:"(string|element)"};class es extends W{constructor(t){super(),this._config=this._getConfig(t)}static get Default(){return Jn}static get DefaultType(){return Zn}static get NAME(){return"TemplateFactory"}getContent(){return Object.values(this._config.content).map(t=>this._resolvePossibleFunction(t)).filter(Boolean)}hasContent(){return this.getContent().length>0}changeContent(t){return this._checkContent(t),this._config.content={...this._config.content,...t},this}toHtml(){const t=document.createElement("div");t.innerHTML=this._maybeSanitize(this._config.template);for(const[e,i]of Object.entries(this._config.content))this._setContent(t,i,e);const e=t.children[0],i=this._resolvePossibleFunction(this._config.extraClass);return i&&e.classList.add(...i.split(" ")),e}_typeCheckConfig(t){super._typeCheckConfig(t),this._checkContent(t.content)}_checkContent(t){for(const[e,i]of Object.entries(t))super._typeCheckConfig({selector:e,entry:i},ts)}_setContent(t,e,i){const n=R.findOne(i,t);n&&((e=this._resolvePossibleFunction(e))?r(e)?this._putElementInTemplate(a(e),n):this._config.html?n.innerHTML=this._maybeSanitize(e):n.textContent=e:n.remove())}_maybeSanitize(t){return this._config.sanitize?function(t,e,i){if(!t.length)return t;if(i&&"function"==typeof i)return i(t);const n=(new window.DOMParser).parseFromString(t,"text/html"),s=[].concat(...n.body.querySelectorAll("*"));for(const t of s){const i=t.nodeName.toLowerCase();if(!Object.keys(e).includes(i)){t.remove();continue}const n=[].concat(...t.attributes),s=[].concat(e["*"]||[],e[i]||[]);for(const e of n)Gn(e,s)||t.removeAttribute(e.nodeName)}return n.body.innerHTML}(t,this._config.allowList,this._config.sanitizeFn):t}_resolvePossibleFunction(t){return _(t,[void 0,this])}_putElementInTemplate(t,e){if(this._config.html)return e.innerHTML="",void e.append(t);e.textContent=t.textContent}}const is=new Set(["sanitize","allowList","sanitizeFn"]),ns="fade",ss="show",os=".tooltip-inner",rs=".modal",as="hide.bs.modal",ls="hover",cs="focus",hs="click",ds={AUTO:"auto",TOP:"top",RIGHT:m()?"left":"right",BOTTOM:"bottom",LEFT:m()?"right":"left"},us={allowList:Xn,animation:!0,boundary:"clippingParents",container:!1,customClass:"",delay:0,fallbackPlacements:["top","right","bottom","left"],html:!1,offset:[0,6],placement:"top",popperConfig:null,sanitize:!0,sanitizeFn:null,selector:!1,template:'<div class="tooltip" role="tooltip"><div class="tooltip-arrow"></div><div class="tooltip-inner"></div></div>',title:"",trigger:"hover focus"},fs={allowList:"object",animation:"boolean",boundary:"(string|element)",container:"(string|element|boolean)",customClass:"(string|function)",delay:"(number|object)",fallbackPlacements:"array",html:"boolean",offset:"(array|string|function)",placement:"(string|function)",popperConfig:"(null|object|function)",sanitize:"boolean",sanitizeFn:"(null|function)",selector:"(string|boolean)",template:"string",title:"(string|element|function)",trigger:"string"};class ps extends B{constructor(t,e){if(void 0===Ai)throw new TypeError("Bootstrap's tooltips require Popper (https://popper.js.org/docs/v2/)");super(t,e),this._isEnabled=!0,this._timeout=0,this._isHovered=null,this._activeTrigger={},this._popper=null,this._templateFactory=null,this._newContent=null,this.tip=null,this._setListeners(),this._config.selector||this._fixTitle()}static get Default(){return us}static get DefaultType(){return fs}static get NAME(){return"tooltip"}enable(){this._isEnabled=!0}disable(){this._isEnabled=!1}toggleEnabled(){this._isEnabled=!this._isEnabled}toggle(){this._isEnabled&&(this._isShown()?this._leave():this._enter())}dispose(){clearTimeout(this._timeout),P.off(this._element.closest(rs),as,this._hideModalHandler),this._element.getAttribute("data-bs-original-title")&&this._element.setAttribute("title",this._element.getAttribute("data-bs-original-title")),this._disposePopper(),super.dispose()}show(){if("none"===this._element.style.display)throw new Error("Please use show on visible elements");if(!this._isWithContent()||!this._isEnabled)return;const t=P.trigger(this._element,this.constructor.eventName("show")),e=(h(this._element)||this._element.ownerDocument.documentElement).contains(this._element);if(t.defaultPrevented||!e)return;this._disposePopper();const i=this._getTipElement();this._element.setAttribute("aria-describedby",i.getAttribute("id"));const{container:n}=this._config;if(this._element.ownerDocument.documentElement.contains(this.tip)||(n.append(i),P.trigger(this._element,this.constructor.eventName("inserted"))),this._popper=this._createPopper(i),i.classList.add(ss),"ontouchstart"in document.documentElement)for(const t of[].concat(...document.body.children))P.on(t,"mouseover",d);this._queueCallback(()=>{P.trigger(this._element,this.constructor.eventName("shown")),!1===this._isHovered&&this._leave(),this._isHovered=!1},this.tip,this._isAnimated())}hide(){if(this._isShown()&&!P.trigger(this._element,this.constructor.eventName("hide")).defaultPrevented){if(this._getTipElement().classList.remove(ss),"ontouchstart"in document.documentElement)for(const t of[].concat(...document.body.children))P.off(t,"mouseover",d);this._activeTrigger[hs]=!1,this._activeTrigger[cs]=!1,this._activeTrigger[ls]=!1,this._isHovered=null,this._queueCallback(()=>{this._isWithActiveTrigger()||(this._isHovered||this._disposePopper(),this._element.removeAttribute("aria-describedby"),P.trigger(this._element,this.constructor.eventName("hidden")))},this.tip,this._isAnimated())}}update(){this._popper&&this._popper.update()}_isWithContent(){return Boolean(this._getTitle())}_getTipElement(){return this.tip||(this.tip=this._createTipElement(this._newContent||this._getContentForTemplate())),this.tip}_createTipElement(t){const e=this._getTemplateFactory(t).toHtml();if(!e)return null;e.classList.remove(ns,ss),e.classList.add(`bs-${this.constructor.NAME}-auto`);const i=(t=>{do{t+=Math.floor(1e6*Math.random())}while(document.getElementById(t));return t})(this.constructor.NAME).toString();return e.setAttribute("id",i),this._isAnimated()&&e.classList.add(ns),e}setContent(t){this._newContent=t,this._isShown()&&(this._disposePopper(),this.show())}_getTemplateFactory(t){return this._templateFactory?this._templateFactory.changeContent(t):this._templateFactory=new es({...this._config,content:t,extraClass:this._resolvePossibleFunction(this._config.customClass)}),this._templateFactory}_getContentForTemplate(){return{[os]:this._getTitle()}}_getTitle(){return this._resolvePossibleFunction(this._config.title)||this._element.getAttribute("data-bs-original-title")}_initializeOnDelegatedTarget(t){return this.constructor.getOrCreateInstance(t.delegateTarget,this._getDelegateConfig())}_isAnimated(){return this._config.animation||this.tip&&this.tip.classList.contains(ns)}_isShown(){return this.tip&&this.tip.classList.contains(ss)}_createPopper(t){const e=_(this._config.placement,[this,t,this._element]),i=ds[e.toUpperCase()];return wi(this._element,t,this._getPopperConfig(i))}_getOffset(){const{offset:t}=this._config;return"string"==typeof t?t.split(",").map(t=>Number.parseInt(t,10)):"function"==typeof t?e=>t(e,this._element):t}_resolvePossibleFunction(t){return _(t,[this._element,this._element])}_getPopperConfig(t){const e={placement:t,modifiers:[{name:"flip",options:{fallbackPlacements:this._config.fallbackPlacements}},{name:"offset",options:{offset:this._getOffset()}},{name:"preventOverflow",options:{boundary:this._config.boundary}},{name:"arrow",options:{element:`.${this.constructor.NAME}-arrow`}},{name:"preSetPlacement",enabled:!0,phase:"beforeMain",fn:t=>{this._getTipElement().setAttribute("data-popper-placement",t.state.placement)}}]};return{...e,..._(this._config.popperConfig,[void 0,e])}}_setListeners(){const t=this._config.trigger.split(" ");for(const e of t)if("click"===e)P.on(this._element,this.constructor.eventName("click"),this._config.selector,t=>{const e=this._initializeOnDelegatedTarget(t);e._activeTrigger[hs]=!(e._isShown()&&e._activeTrigger[hs]),e.toggle()});else if("manual"!==e){const t=e===ls?this.constructor.eventName("mouseenter"):this.constructor.eventName("focusin"),i=e===ls?this.constructor.eventName("mouseleave"):this.constructor.eventName("focusout");P.on(this._element,t,this._config.selector,t=>{const e=this._initializeOnDelegatedTarget(t);e._activeTrigger["focusin"===t.type?cs:ls]=!0,e._enter()}),P.on(this._element,i,this._config.selector,t=>{const e=this._initializeOnDelegatedTarget(t);e._activeTrigger["focusout"===t.type?cs:ls]=e._element.contains(t.relatedTarget),e._leave()})}this._hideModalHandler=()=>{this._element&&this.hide()},P.on(this._element.closest(rs),as,this._hideModalHandler)}_fixTitle(){const t=this._element.getAttribute("title");t&&(this._element.getAttribute("aria-label")||this._element.textContent.trim()||this._element.setAttribute("aria-label",t),this._element.setAttribute("data-bs-original-title",t),this._element.removeAttribute("title"))}_enter(){this._isShown()||this._isHovered?this._isHovered=!0:(this._isHovered=!0,this._setTimeout(()=>{this._isHovered&&this.show()},this._config.delay.show))}_leave(){this._isWithActiveTrigger()||(this._isHovered=!1,this._setTimeout(()=>{this._isHovered||this.hide()},this._config.delay.hide))}_setTimeout(t,e){clearTimeout(this._timeout),this._timeout=setTimeout(t,e)}_isWithActiveTrigger(){return Object.values(this._activeTrigger).includes(!0)}_getConfig(t){const e=H.getDataAttributes(this._element);for(const t of Object.keys(e))is.has(t)&&delete e[t];return t={...e,..."object"==typeof t&&t?t:{}},t=this._mergeConfigObj(t),t=this._configAfterMerge(t),this._typeCheckConfig(t),t}_configAfterMerge(t){return t.container=!1===t.container?document.body:a(t.container),"number"==typeof t.delay&&(t.delay={show:t.delay,hide:t.delay}),"number"==typeof t.title&&(t.title=t.title.toString()),"number"==typeof t.content&&(t.content=t.content.toString()),t}_getDelegateConfig(){const t={};for(const[e,i]of Object.entries(this._config))this.constructor.Default[e]!==i&&(t[e]=i);return t.selector=!1,t.trigger="manual",t}_disposePopper(){this._popper&&(this._popper.destroy(),this._popper=null),this.tip&&(this.tip.remove(),this.tip=null)}static jQueryInterface(t){return this.each(function(){const e=ps.getOrCreateInstance(this,t);if("string"==typeof t){if(void 0===e[t])throw new TypeError(`No method named "${t}"`);e[t]()}})}}g(ps);const ms=".popover-header",gs=".popover-body",_s={...ps.Default,content:"",offset:[0,8],placement:"right",template:'<div class="popover" role="tooltip"><div class="popover-arrow"></div><h3 class="popover-header"></h3><div class="popover-body"></div></div>',trigger:"click"},bs={...ps.DefaultType,content:"(null|string|element|function)"};class vs extends ps{static get Default(){return _s}static get DefaultType(){return bs}static get NAME(){return"popover"}_isWithContent(){return this._getTitle()||this._getContent()}_getContentForTemplate(){return{[ms]:this._getTitle(),[gs]:this._getContent()}}_getContent(){return this._resolvePossibleFunction(this._config.content)}static jQueryInterface(t){return this.each(function(){const e=vs.getOrCreateInstance(this,t);if("string"==typeof t){if(void 0===e[t])throw new TypeError(`No method named "${t}"`);e[t]()}})}}g(vs);const ys=".bs.scrollspy",ws=`activate${ys}`,As=`click${ys}`,Es=`load${ys}.data-api`,Ts="active",Cs="[href]",Os=".nav-link",xs=`${Os}, .nav-item > ${Os}, .list-group-item`,ks={offset:null,rootMargin:"0px 0px -25%",smoothScroll:!1,target:null,threshold:[.1,.5,1]},Ls={offset:"(number|null)",rootMargin:"string",smoothScroll:"boolean",target:"element",threshold:"array"};class Ss extends B{constructor(t,e){super(t,e),this._targetLinks=new Map,this._observableSections=new Map,this._rootElement="visible"===getComputedStyle(this._element).overflowY?null:this._element,this._activeTarget=null,this._observer=null,this._previousScrollData={visibleEntryTop:0,parentScrollTop:0},this.refresh()}static get Default(){return ks}static get DefaultType(){return Ls}static get NAME(){return"scrollspy"}refresh(){this._initializeTargetsAndObservables(),this._maybeEnableSmoothScroll(),this._observer?this._observer.disconnect():this._observer=this._getNewObserver();for(const t of this._observableSections.values())this._observer.observe(t)}dispose(){this._observer.disconnect(),super.dispose()}_configAfterMerge(t){return t.target=a(t.target)||document.body,t.rootMargin=t.offset?`${t.offset}px 0px -30%`:t.rootMargin,"string"==typeof t.threshold&&(t.threshold=t.threshold.split(",").map(t=>Number.parseFloat(t))),t}_maybeEnableSmoothScroll(){this._config.smoothScroll&&(P.off(this._config.target,As),P.on(this._config.target,As,Cs,t=>{const e=this._observableSections.get(t.target.hash);if(e){t.preventDefault();const i=this._rootElement||window,n=e.offsetTop-this._element.offsetTop;if(i.scrollTo)return void i.scrollTo({top:n,behavior:"smooth"});i.scrollTop=n}}))}_getNewObserver(){const t={root:this._rootElement,threshold:this._config.threshold,rootMargin:this._config.rootMargin};return new IntersectionObserver(t=>this._observerCallback(t),t)}_observerCallback(t){const e=t=>this._targetLinks.get(`#${t.target.id}`),i=t=>{this._previousScrollData.visibleEntryTop=t.target.offsetTop,this._process(e(t))},n=(this._rootElement||document.documentElement).scrollTop,s=n>=this._previousScrollData.parentScrollTop;this._previousScrollData.parentScrollTop=n;for(const o of t){if(!o.isIntersecting){this._activeTarget=null,this._clearActiveClass(e(o));continue}const t=o.target.offsetTop>=this._previousScrollData.visibleEntryTop;if(s&&t){if(i(o),!n)return}else s||t||i(o)}}_initializeTargetsAndObservables(){this._targetLinks=new Map,this._observableSections=new Map;const t=R.find(Cs,this._config.target);for(const e of t){if(!e.hash||c(e))continue;const t=R.findOne(decodeURI(e.hash),this._element);l(t)&&(this._targetLinks.set(decodeURI(e.hash),e),this._observableSections.set(e.hash,t))}}_process(t){this._activeTarget!==t&&(this._clearActiveClass(this._config.target),this._activeTarget=t,t.classList.add(Ts),this._activateParents(t),P.trigger(this._element,ws,{relatedTarget:t}))}_activateParents(t){if(t.classList.contains("dropdown-item"))R.findOne(".dropdown-toggle",t.closest(".dropdown")).classList.add(Ts);else for(const e of R.parents(t,".nav, .list-group"))for(const t of R.prev(e,xs))t.classList.add(Ts)}_clearActiveClass(t){t.classList.remove(Ts);const e=R.find(`${Cs}.${Ts}`,t);for(const t of e)t.classList.remove(Ts)}static jQueryInterface(t){return this.each(function(){const e=Ss.getOrCreateInstance(this,t);if("string"==typeof t){if(void 0===e[t]||t.startsWith("_")||"constructor"===t)throw new TypeError(`No method named "${t}"`);e[t]()}})}}P.on(window,Es,()=>{for(const t of R.find('[data-bs-spy="scroll"]'))Ss.getOrCreateInstance(t)}),g(Ss);const Ds=".bs.tab",$s=`hide${Ds}`,Is=`hidden${Ds}`,Ns=`show${Ds}`,Ps=`shown${Ds}`,js=`click${Ds}`,Ms=`keydown${Ds}`,Fs=`load${Ds}`,Hs="ArrowLeft",Ws="ArrowRight",Bs="ArrowUp",zs="ArrowDown",Rs="Home",qs="End",Vs="active",Ks="fade",Qs="show",Xs=".dropdown-toggle",Ys=`:not(${Xs})`,Us='[data-bs-toggle="tab"], [data-bs-toggle="pill"], [data-bs-toggle="list"]',Gs=`.nav-link${Ys}, .list-group-item${Ys}, [role="tab"]${Ys}, ${Us}`,Js=`.${Vs}[data-bs-toggle="tab"], .${Vs}[data-bs-toggle="pill"], .${Vs}[data-bs-toggle="list"]`;class Zs extends B{constructor(t){super(t),this._parent=this._element.closest('.list-group, .nav, [role="tablist"]'),this._parent&&(this._setInitialAttributes(this._parent,this._getChildren()),P.on(this._element,Ms,t=>this._keydown(t)))}static get NAME(){return"tab"}show(){const t=this._element;if(this._elemIsActive(t))return;const e=this._getActiveElem(),i=e?P.trigger(e,$s,{relatedTarget:t}):null;P.trigger(t,Ns,{relatedTarget:e}).defaultPrevented||i&&i.defaultPrevented||(this._deactivate(e,t),this._activate(t,e))}_activate(t,e){t&&(t.classList.add(Vs),this._activate(R.getElementFromSelector(t)),this._queueCallback(()=>{"tab"===t.getAttribute("role")?(t.removeAttribute("tabindex"),t.setAttribute("aria-selected",!0),this._toggleDropDown(t,!0),P.trigger(t,Ps,{relatedTarget:e})):t.classList.add(Qs)},t,t.classList.contains(Ks)))}_deactivate(t,e){t&&(t.classList.remove(Vs),t.blur(),this._deactivate(R.getElementFromSelector(t)),this._queueCallback(()=>{"tab"===t.getAttribute("role")?(t.setAttribute("aria-selected",!1),t.setAttribute("tabindex","-1"),this._toggleDropDown(t,!1),P.trigger(t,Is,{relatedTarget:e})):t.classList.remove(Qs)},t,t.classList.contains(Ks)))}_keydown(t){if(![Hs,Ws,Bs,zs,Rs,qs].includes(t.key))return;t.stopPropagation(),t.preventDefault();const e=this._getChildren().filter(t=>!c(t));let i;if([Rs,qs].includes(t.key))i=e[t.key===Rs?0:e.length-1];else{const n=[Ws,zs].includes(t.key);i=v(e,t.target,n,!0)}i&&(i.focus({preventScroll:!0}),Zs.getOrCreateInstance(i).show())}_getChildren(){return R.find(Gs,this._parent)}_getActiveElem(){return this._getChildren().find(t=>this._elemIsActive(t))||null}_setInitialAttributes(t,e){this._setAttributeIfNotExists(t,"role","tablist");for(const t of e)this._setInitialAttributesOnChild(t)}_setInitialAttributesOnChild(t){t=this._getInnerElement(t);const e=this._elemIsActive(t),i=this._getOuterElement(t);t.setAttribute("aria-selected",e),i!==t&&this._setAttributeIfNotExists(i,"role","presentation"),e||t.setAttribute("tabindex","-1"),this._setAttributeIfNotExists(t,"role","tab"),this._setInitialAttributesOnTargetPanel(t)}_setInitialAttributesOnTargetPanel(t){const e=R.getElementFromSelector(t);e&&(this._setAttributeIfNotExists(e,"role","tabpanel"),t.id&&this._setAttributeIfNotExists(e,"aria-labelledby",`${t.id}`))}_toggleDropDown(t,e){const i=this._getOuterElement(t);if(!i.classList.contains("dropdown"))return;const n=(t,n)=>{const s=R.findOne(t,i);s&&s.classList.toggle(n,e)};n(Xs,Vs),n(".dropdown-menu",Qs),i.setAttribute("aria-expanded",e)}_setAttributeIfNotExists(t,e,i){t.hasAttribute(e)||t.setAttribute(e,i)}_elemIsActive(t){return t.classList.contains(Vs)}_getInnerElement(t){return t.matches(Gs)?t:R.findOne(Gs,t)}_getOuterElement(t){return t.closest(".nav-item, .list-group-item")||t}static jQueryInterface(t){return this.each(function(){const e=Zs.getOrCreateInstance(this);if("string"==typeof t){if(void 0===e[t]||t.startsWith("_")||"constructor"===t)throw new TypeError(`No method named "${t}"`);e[t]()}})}}P.on(document,js,Us,function(t){["A","AREA"].includes(this.tagName)&&t.preventDefault(),c(this)||Zs.getOrCreateInstance(this).show()}),P.on(window,Fs,()=>{for(const t of R.find(Js))Zs.getOrCreateInstance(t)}),g(Zs);const to=".bs.toast",eo=`mouseover${to}`,io=`mouseout${to}`,no=`focusin${to}`,so=`focusout${to}`,oo=`hide${to}`,ro=`hidden${to}`,ao=`show${to}`,lo=`shown${to}`,co="hide",ho="show",uo="showing",fo={animation:"boolean",autohide:"boolean",delay:"number"},po={animation:!0,autohide:!0,delay:5e3};class mo extends B{constructor(t,e){super(t,e),this._timeout=null,this._hasMouseInteraction=!1,this._hasKeyboardInteraction=!1,this._setListeners()}static get Default(){return po}static get DefaultType(){return fo}static get NAME(){return"toast"}show(){P.trigger(this._element,ao).defaultPrevented||(this._clearTimeout(),this._config.animation&&this._element.classList.add("fade"),this._element.classList.remove(co),u(this._element),this._element.classList.add(ho,uo),this._queueCallback(()=>{this._element.classList.remove(uo),P.trigger(this._element,lo),this._maybeScheduleHide()},this._element,this._config.animation))}hide(){this.isShown()&&(P.trigger(this._element,oo).defaultPrevented||(this._element.classList.add(uo),this._queueCallback(()=>{this._element.classList.add(co),this._element.classList.remove(uo,ho),P.trigger(this._element,ro)},this._element,this._config.animation)))}dispose(){this._clearTimeout(),this.isShown()&&this._element.classList.remove(ho),super.dispose()}isShown(){return this._element.classList.contains(ho)}_maybeScheduleHide(){this._config.autohide&&(this._hasMouseInteraction||this._hasKeyboardInteraction||(this._timeout=setTimeout(()=>{this.hide()},this._config.delay)))}_onInteraction(t,e){switch(t.type){case"mouseover":case"mouseout":this._hasMouseInteraction=e;break;case"focusin":case"focusout":this._hasKeyboardInteraction=e}if(e)return void this._clearTimeout();const i=t.relatedTarget;this._element===i||this._element.contains(i)||this._maybeScheduleHide()}_setListeners(){P.on(this._element,eo,t=>this._onInteraction(t,!0)),P.on(this._element,io,t=>this._onInteraction(t,!1)),P.on(this._element,no,t=>this._onInteraction(t,!0)),P.on(this._element,so,t=>this._onInteraction(t,!1))}_clearTimeout(){clearTimeout(this._timeout),this._timeout=null}static jQueryInterface(t){return this.each(function(){const e=mo.getOrCreateInstance(this,t);if("string"==typeof t){if(void 0===e[t])throw new TypeError(`No method named "${t}"`);e[t](this)}})}}return q(mo),g(mo),{Alert:X,Button:U,Carousel:St,Collapse:qt,Dropdown:Qi,Modal:Ln,Offcanvas:Qn,Popover:vs,ScrollSpy:Ss,Tab:Zs,Toast:mo,Tooltip:ps}});

// @license magnet:?xt=urn:btih:d3d9a9a6595521f9666a5e94cc830dab83b65699&dn=expat.txt Expat
//
// AnchorJS - v5.0.0 - 2023-01-18
// https://www.bryanbraun.com/anchorjs/
// Copyright (c) 2023 Bryan Braun; Licensed MIT
//
// @license magnet:?xt=urn:btih:d3d9a9a6595521f9666a5e94cc830dab83b65699&dn=expat.txt Expat
!function(A,e){"use strict";"function"==typeof define&&define.amd?define([],e):"object"==typeof module&&module.exports?module.exports=e():(A.AnchorJS=e(),A.anchors=new A.AnchorJS)}(globalThis,function(){"use strict";return function(A){function u(A){A.icon=Object.prototype.hasOwnProperty.call(A,"icon")?A.icon:"",A.visible=Object.prototype.hasOwnProperty.call(A,"visible")?A.visible:"hover",A.placement=Object.prototype.hasOwnProperty.call(A,"placement")?A.placement:"right",A.ariaLabel=Object.prototype.hasOwnProperty.call(A,"ariaLabel")?A.ariaLabel:"Anchor",A.class=Object.prototype.hasOwnProperty.call(A,"class")?A.class:"",A.base=Object.prototype.hasOwnProperty.call(A,"base")?A.base:"",A.truncate=Object.prototype.hasOwnProperty.call(A,"truncate")?Math.floor(A.truncate):64,A.titleText=Object.prototype.hasOwnProperty.call(A,"titleText")?A.titleText:""}function d(A){var e;if("string"==typeof A||A instanceof String)e=[].slice.call(document.querySelectorAll(A));else{if(!(Array.isArray(A)||A instanceof NodeList))throw new TypeError("The selector provided to AnchorJS was invalid.");e=[].slice.call(A)}return e}this.options=A||{},this.elements=[],u(this.options),this.add=function(A){var e,t,o,i,n,s,a,r,l,c,h,p=[];if(u(this.options),0!==(e=d(A=A||"h2, h3, h4, h5, h6")).length){for(null===document.head.querySelector("style.anchorjs")&&((A=document.createElement("style")).className="anchorjs",A.appendChild(document.createTextNode("")),void 0===(h=document.head.querySelector('[rel="stylesheet"],style'))?document.head.appendChild(A):document.head.insertBefore(A,h),A.sheet.insertRule(".anchorjs-link{opacity:0;text-decoration:none;-webkit-font-smoothing:antialiased;-moz-osx-font-smoothing:grayscale}",A.sheet.cssRules.length),A.sheet.insertRule(":hover>.anchorjs-link,.anchorjs-link:focus{opacity:1}",A.sheet.cssRules.length),A.sheet.insertRule("[data-anchorjs-icon]::after{content:attr(data-anchorjs-icon)}",A.sheet.cssRules.length),A.sheet.insertRule('@font-face{font-family:anchorjs-icons;src:url(data:n/a;base64,AAEAAAALAIAAAwAwT1MvMg8yG2cAAAE4AAAAYGNtYXDp3gC3AAABpAAAAExnYXNwAAAAEAAAA9wAAAAIZ2x5ZlQCcfwAAAH4AAABCGhlYWQHFvHyAAAAvAAAADZoaGVhBnACFwAAAPQAAAAkaG10eASAADEAAAGYAAAADGxvY2EACACEAAAB8AAAAAhtYXhwAAYAVwAAARgAAAAgbmFtZQGOH9cAAAMAAAAAunBvc3QAAwAAAAADvAAAACAAAQAAAAEAAHzE2p9fDzz1AAkEAAAAAADRecUWAAAAANQA6R8AAAAAAoACwAAAAAgAAgAAAAAAAAABAAADwP/AAAACgAAA/9MCrQABAAAAAAAAAAAAAAAAAAAAAwABAAAAAwBVAAIAAAAAAAIAAAAAAAAAAAAAAAAAAAAAAAMCQAGQAAUAAAKZAswAAACPApkCzAAAAesAMwEJAAAAAAAAAAAAAAAAAAAAARAAAAAAAAAAAAAAAAAAAAAAQAAg//0DwP/AAEADwABAAAAAAQAAAAAAAAAAAAAAIAAAAAAAAAIAAAACgAAxAAAAAwAAAAMAAAAcAAEAAwAAABwAAwABAAAAHAAEADAAAAAIAAgAAgAAACDpy//9//8AAAAg6cv//f///+EWNwADAAEAAAAAAAAAAAAAAAAACACEAAEAAAAAAAAAAAAAAAAxAAACAAQARAKAAsAAKwBUAAABIiYnJjQ3NzY2MzIWFxYUBwcGIicmNDc3NjQnJiYjIgYHBwYUFxYUBwYGIwciJicmNDc3NjIXFhQHBwYUFxYWMzI2Nzc2NCcmNDc2MhcWFAcHBgYjARQGDAUtLXoWOR8fORYtLTgKGwoKCjgaGg0gEhIgDXoaGgkJBQwHdR85Fi0tOAobCgoKOBoaDSASEiANehoaCQkKGwotLXoWOR8BMwUFLYEuehYXFxYugC44CQkKGwo4GkoaDQ0NDXoaShoKGwoFBe8XFi6ALjgJCQobCjgaShoNDQ0NehpKGgobCgoKLYEuehYXAAAADACWAAEAAAAAAAEACAAAAAEAAAAAAAIAAwAIAAEAAAAAAAMACAAAAAEAAAAAAAQACAAAAAEAAAAAAAUAAQALAAEAAAAAAAYACAAAAAMAAQQJAAEAEAAMAAMAAQQJAAIABgAcAAMAAQQJAAMAEAAMAAMAAQQJAAQAEAAMAAMAAQQJAAUAAgAiAAMAAQQJAAYAEAAMYW5jaG9yanM0MDBAAGEAbgBjAGgAbwByAGoAcwA0ADAAMABAAAAAAwAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAABAAH//wAP) format("truetype")}',A.sheet.cssRules.length)),h=document.querySelectorAll("[id]"),t=[].map.call(h,function(A){return A.id}),i=0;i<e.length;i++)if(this.hasAnchorJSLink(e[i]))p.push(i);else{if(e[i].hasAttribute("id"))o=e[i].getAttribute("id");else if(e[i].hasAttribute("data-anchor-id"))o=e[i].getAttribute("data-anchor-id");else{for(r=a=this.urlify(e[i].textContent),s=0;n=t.indexOf(r=void 0!==n?a+"-"+s:r),s+=1,-1!==n;);n=void 0,t.push(r),e[i].setAttribute("id",r),o=r}(l=document.createElement("a")).className="anchorjs-link "+this.options.class,l.setAttribute("aria-label",this.options.ariaLabel),l.setAttribute("data-anchorjs-icon",this.options.icon),this.options.titleText&&(l.title=this.options.titleText),c=document.querySelector("base")?window.location.pathname+window.location.search:"",c=this.options.base||c,l.href=c+"#"+o,"always"===this.options.visible&&(l.style.opacity="1"),""===this.options.icon&&(l.style.font="1em/1 anchorjs-icons","left"===this.options.placement)&&(l.style.lineHeight="inherit"),"left"===this.options.placement?(l.style.position="absolute",l.style.marginLeft="-1.25em",l.style.paddingRight=".25em",l.style.paddingLeft=".25em",e[i].insertBefore(l,e[i].firstChild)):(l.style.marginLeft=".1875em",l.style.paddingRight=".1875em",l.style.paddingLeft=".1875em",e[i].appendChild(l))}for(i=0;i<p.length;i++)e.splice(p[i]-i,1);this.elements=this.elements.concat(e)}return this},this.remove=function(A){for(var e,t,o=d(A),i=0;i<o.length;i++)(t=o[i].querySelector(".anchorjs-link"))&&(-1!==(e=this.elements.indexOf(o[i]))&&this.elements.splice(e,1),o[i].removeChild(t));return this},this.removeAll=function(){this.remove(this.elements)},this.urlify=function(A){var e=document.createElement("textarea");return e.innerHTML=A,A=e.value,this.options.truncate||u(this.options),A.trim().replace(/'/gi,"").replace(/[& +$,:;=?@"#{}|^~[`%!'<>\]./()*\\\n\t\b\v\u00A0]/g,"-").replace(/-{2,}/g,"-").substring(0,this.options.truncate).replace(/^-+|-+$/gm,"").toLowerCase()},this.hasAnchorJSLink=function(A){var e=A.firstChild&&-1<(" "+A.firstChild.className+" ").indexOf(" anchorjs-link "),A=A.lastChild&&-1<(" "+A.lastChild.className+" ").indexOf(" anchorjs-link ");return e||A||!1}}});
// @license-end
(()=>{"use strict";var e={d:(t,n)=>{for(var o in n)e.o(n,o)&&!e.o(t,o)&&Object.defineProperty(t,o,{enumerable:!0,get:n[o]})},o:(e,t)=>Object.prototype.hasOwnProperty.call(e,t),r:e=>{"undefined"!=typeof Symbol&&Symbol.toStringTag&&Object.defineProperty(e,Symbol.toStringTag,{value:"Module"}),Object.defineProperty(e,"__esModule",{value:!0})}},t={};function n(e){const t=[].forEach,n=[].some,o="undefined"!=typeof window&&document.body,l=" ";let r,s=!0,i=0;function c(n,o){const r=o.appendChild(function(n){const o=document.createElement("li"),r=document.createElement("a");return e.listItemClass&&o.setAttribute("class",e.listItemClass),e.onClick&&(r.onclick=e.onClick),e.includeTitleTags&&r.setAttribute("title",n.textContent),e.includeHtml&&n.childNodes.length?t.call(n.childNodes,(e=>{r.appendChild(e.cloneNode(!0))})):r.textContent=n.textContent,r.setAttribute("href",`${e.basePath}#${n.id}`),r.setAttribute("class",`${e.linkClass+l}node-name--${n.nodeName}${l}${e.extraLinkClasses}`),o.appendChild(r),o}(n));if(n.children.length){const e=a(n.isCollapsed);n.children.forEach((t=>{c(t,e)})),r.appendChild(e)}}function a(t){const n=e.orderedList?"ol":"ul",o=document.createElement(n);let r=e.listClass+l+e.extraListClasses;return t&&(r=r+l+e.collapsibleClass,r=r+l+e.isCollapsedClass),o.setAttribute("class",r),o}function d(t){let n=0;return null!==t&&(n=t.offsetTop,e.hasInnerContainers&&(n+=d(t.offsetParent))),n}function u(e,t){return e&&e.className!==t&&(e.className=t),e}function f(t){return t&&-1!==t.className.indexOf(e.collapsibleClass)&&-1!==t.className.indexOf(e.isCollapsedClass)?(u(t,t.className.replace(l+e.isCollapsedClass,"")),f(t.parentNode.parentNode)):t}function m(t){const n=p(),o=document?.getElementById(t);return o.offsetTop>n.offsetHeight-1.4*n.clientHeight-e.bottomModeThreshold}function h(){const t=p(),n=t.scrollHeight>t.clientHeight,o=g()+t.clientHeight>t.offsetHeight-e.bottomModeThreshold;return n&&o}function p(){let t;return t=e.scrollContainer&&document.querySelector(e.scrollContainer)?document.querySelector(e.scrollContainer):document.documentElement||o,t}function g(){const e=p();return e?.scrollTop||0}function C(t,o=g()){let l;return n.call(t,((n,r)=>d(n)>o+e.headingsOffset+10?(l=t[0===r?r:r-1],!0):r===t.length-1?(l=t[t.length-1],!0):void 0)),l}return{enableTocAnimation:function(){s=!0},disableTocAnimation:function(t){const n=t.target||t.srcElement;"string"==typeof n.className&&-1!==n.className.indexOf(e.linkClass)&&(s=!1)},render:function(e,t){const n=a(!1);if(t.forEach((e=>{c(e,n)})),r=e||r,null!==r)return r.firstChild&&r.removeChild(r.firstChild),0===t.length?r:r.appendChild(n)},updateToc:function(n,o){e.positionFixedSelector&&function(){const t=g(),n=document.querySelector(e.positionFixedSelector);"auto"===e.fixedSidebarOffset&&(e.fixedSidebarOffset=r.offsetTop),t>e.fixedSidebarOffset?-1===n.className.indexOf(e.positionFixedClass)&&(n.className+=l+e.positionFixedClass):n.className=n.className.replace(l+e.positionFixedClass,"")}();const c=n,a=o?.target?.getAttribute?o?.target?.getAttribute("href"):null,d=!(!a||"#"!==a.charAt(0))&&m(a.replace("#",""));if(o&&i<5&&i++,(s||d)&&r&&c.length>0){const n=C(c),o=r.querySelector(`.${e.activeLinkClass}`),s=n.id.replace(/([ #;&,.+*~':"!^$[\]()=>|/\\@])/g,"\\$1"),p=window.location.hash.replace("#","");let g=s;const b=h();a&&d?g=a.replace("#",""):p&&p!==s&&b&&(m(s)||i<=2)&&(g=p);const S=r.querySelector(`.${e.linkClass}[href="${e.basePath}#${g}"]`);if(o===S)return;const y=r.querySelectorAll(`.${e.linkClass}`);t.call(y,(t=>{u(t,t.className.replace(l+e.activeLinkClass,""))}));const T=r.querySelectorAll(`.${e.listItemClass}`);t.call(T,(t=>{u(t,t.className.replace(l+e.activeListItemClass,""))})),S&&-1===S.className.indexOf(e.activeLinkClass)&&(S.className+=l+e.activeLinkClass);const v=S?.parentNode;v&&-1===v.className.indexOf(e.activeListItemClass)&&(v.className+=l+e.activeListItemClass);const w=r.querySelectorAll(`.${e.listClass}.${e.collapsibleClass}`);t.call(w,(t=>{-1===t.className.indexOf(e.isCollapsedClass)&&(t.className+=l+e.isCollapsedClass)})),S?.nextSibling&&-1!==S.nextSibling.className.indexOf(e.isCollapsedClass)&&u(S.nextSibling,S.nextSibling.className.replace(l+e.isCollapsedClass,"")),f(S?.parentNode.parentNode)}},getCurrentlyHighlighting:function(){return s},getTopHeader:C,getScrollTop:g,updateUrlHashForHeader:function(e){const t=g(),n=C(e,t),o=h();if(n&&!(t<5)||o){if(n&&!o){const e=`#${n.id}`;window.location.hash!==e&&window.history.pushState(null,null,e)}}else"#"!==window.location.hash&&""!==window.location.hash&&window.history.pushState(null,null,"#")}}}e.r(t),e.d(t,{_buildHtml:()=>r,_headingsArray:()=>i,_options:()=>d,_parseContent:()=>s,_scrollListener:()=>c,default:()=>b,destroy:()=>f,init:()=>u,refresh:()=>m});const o={tocSelector:".js-toc",tocElement:null,contentSelector:".js-toc-content",contentElement:null,headingSelector:"h1, h2, h3",ignoreSelector:".js-toc-ignore",hasInnerContainers:!1,linkClass:"toc-link",extraLinkClasses:"",activeLinkClass:"is-active-link",listClass:"toc-list",extraListClasses:"",isCollapsedClass:"is-collapsed",collapsibleClass:"is-collapsible",listItemClass:"toc-list-item",activeListItemClass:"is-active-li",collapseDepth:0,scrollSmooth:!0,scrollSmoothDuration:420,scrollSmoothOffset:0,scrollEndCallback:function(e){},headingsOffset:1,enableUrlHashUpdateOnScroll:!1,scrollHandlerType:"auto",scrollHandlerTimeout:50,throttleTimeout:50,positionFixedSelector:null,positionFixedClass:"is-position-fixed",fixedSidebarOffset:"auto",includeHtml:!1,includeTitleTags:!1,onClick:function(e){},orderedList:!0,scrollContainer:null,skipRendering:!1,headingLabelCallback:!1,ignoreHiddenElements:!1,headingObjectCallback:null,basePath:"",disableTocScrollSync:!1,tocScrollingWrapper:null,tocScrollOffset:30,bottomModeThreshold:30};function l(e){var t=e.duration,n=e.offset;if("undefined"!=typeof window&&"undefined"!=typeof location){var o=location.hash?l(location.href):location.href;document.body.addEventListener("click",(function(r){var s;"a"!==(s=r.target).tagName.toLowerCase()||!(s.hash.length>0||"#"===s.href.charAt(s.href.length-1))||l(s.href)!==o&&l(s.href)+"#"!==o||r.target.className.indexOf("no-smooth-scroll")>-1||"#"===r.target.href.charAt(r.target.href.length-2)&&"!"===r.target.href.charAt(r.target.href.length-1)||-1===r.target.className.indexOf(e.linkClass)||function(e,t){var n,o,l=window.pageYOffset,r={duration:t.duration,offset:t.offset||0,callback:t.callback,easing:t.easing||function(e,t,n,o){return(e/=o/2)<1?n/2*e*e+t:-n/2*(--e*(e-2)-1)+t}},s=document.querySelector('[id="'+decodeURI(e).split("#").join("")+'"]')||document.querySelector('[id="'+e.split("#").join("")+'"]'),i="string"==typeof e?r.offset+(e?s&&s.getBoundingClientRect().top||0:-(document.documentElement.scrollTop||document.body.scrollTop)):e,c="function"==typeof r.duration?r.duration(i):r.duration;function a(e){o=e-n,window.scrollTo(0,r.easing(o,l,i,c)),o<c?requestAnimationFrame(a):(window.scrollTo(0,l+i),"function"==typeof r.callback&&r.callback())}requestAnimationFrame((function(e){n=e,a(e)}))}(r.target.hash,{duration:t,offset:n,callback:function(){var e,t;e=r.target.hash,(t=document.getElementById(e.substring(1)))&&(/^(?:a|select|input|button|textarea)$/i.test(t.tagName)||(t.tabIndex=-1),t.focus())}})}),!1)}function l(e){return e.slice(0,e.lastIndexOf("#"))}}let r,s,i,c,a,d={};function u(e){let t=!1;d=function(...e){const t={};for(let n=0;n<e.length;n++){const o=e[n];for(const e in o)h.call(o,e)&&(t[e]=o[e])}return t}(o,e||{}),d.scrollSmooth&&(d.duration=d.scrollSmoothDuration,d.offset=d.scrollSmoothOffset,l(d)),r=n(d),s=function(e){const t=[].reduce;function n(e){return e[e.length-1]}function o(e){return+e.nodeName.toUpperCase().replace("H","")}function l(t){if(!function(e){try{return e instanceof window.HTMLElement||e instanceof window.parent.HTMLElement}catch(t){return e instanceof window.HTMLElement}}(t))return t;if(e.ignoreHiddenElements&&(!t.offsetHeight||!t.offsetParent))return null;const n=t.getAttribute("data-heading-label")||(e.headingLabelCallback?String(e.headingLabelCallback(t.innerText)):(t.innerText||t.textContent).trim()),l={id:t.id,children:[],nodeName:t.nodeName,headingLevel:o(t),textContent:n};return e.includeHtml&&(l.childNodes=t.childNodes),e.headingObjectCallback?e.headingObjectCallback(l,t):l}return{nestHeadingsArray:function(o){return t.call(o,(function(t,o){const r=l(o);return r&&function(t,o){const r=l(t),s=r.headingLevel;let i=o,c=n(i),a=s-(c?c.headingLevel:0);for(;a>0&&(c=n(i),!c||s!==c.headingLevel);)c&&void 0!==c.children&&(i=c.children),a--;s>=e.collapseDepth&&(r.isCollapsed=!0),i.push(r)}(r,t.nest),t}),{nest:[]})},selectHeadings:function(t,n){let o=n;e.ignoreSelector&&(o=n.split(",").map((function(t){return`${t.trim()}:not(${e.ignoreSelector})`})));try{return t.querySelectorAll(o)}catch(e){return console.warn(`Headers not found with selector: ${o}`),null}}}}(d),f();const u=function(e){try{return e.contentElement||document.querySelector(e.contentSelector)}catch(t){return console.warn(`Contents element not found: ${e.contentSelector}`),null}}(d);if(null===u)return;const m=C(d);if(null===m)return;if(i=s.selectHeadings(u,d.headingSelector),null===i)return;const b=s.nestHeadingsArray(i).nest;if(d.skipRendering)return this;r.render(m,b);let S=!1;const y=d.scrollHandlerTimeout||d.throttleTimeout;c=function(e,t,n="auto"){switch(n){case"debounce":return g(e,t);case"throttle":return p(e,t);default:return t<334?g(e,t):p(e,t)}}((e=>{r.updateToc(i,e),!d.disableTocScrollSync&&!S&&function(e){const t=e.tocScrollingWrapper||e.tocElement||document.querySelector(e.tocSelector);if(t&&t.scrollHeight>t.clientHeight){const n=t.querySelector(`.${e.activeListItemClass}`);if(n){const o=n.offsetTop-e.tocScrollOffset;t.scrollTop=o>0?o:0}}}(d),d.enableUrlHashUpdateOnScroll&&t&&r.getCurrentlyHighlighting()&&r.updateUrlHashForHeader(i);const n=0===e?.target?.scrollingElement?.scrollTop;(e&&(0===e.eventPhase||null===e.currentTarget)||n)&&(r.updateToc(i),d.scrollEndCallback?.(e))}),y,d.scrollHandlerType),t||(c(),t=!0),window.onhashchange=window.onscrollend=e=>{c(e)},d.scrollContainer&&document.querySelector(d.scrollContainer)?(document.querySelector(d.scrollContainer).addEventListener("scroll",c,!1),document.querySelector(d.scrollContainer).addEventListener("resize",c,!1)):(document.addEventListener("scroll",c,!1),document.addEventListener("resize",c,!1));let T=null;a=p((e=>{S=!0,d.scrollSmooth&&r.disableTocAnimation(e),r.updateToc(i,e),T&&clearTimeout(T),T=setTimeout((()=>{r.enableTocAnimation()}),d.scrollSmoothDuration),setTimeout((()=>{S=!1}),d.scrollSmoothDuration+100)}),d.throttleTimeout),d.scrollContainer&&document.querySelector(d.scrollContainer)?document.querySelector(d.scrollContainer).addEventListener("click",a,!1):document.addEventListener("click",a,!1)}function f(){const e=C(d);null!==e&&(d.skipRendering||e&&(e.innerHTML=""),d.scrollContainer&&document.querySelector(d.scrollContainer)?(document.querySelector(d.scrollContainer).removeEventListener("scroll",c,!1),document.querySelector(d.scrollContainer).removeEventListener("resize",c,!1),r&&document.querySelector(d.scrollContainer).removeEventListener("click",a,!1)):(document.removeEventListener("scroll",c,!1),document.removeEventListener("resize",c,!1),r&&document.removeEventListener("click",a,!1)))}function m(e){f(),u(e||d)}const h=Object.prototype.hasOwnProperty;function p(e,t,n){let o,l;return t||(t=250),function(...r){const s=n||this,i=+new Date;o&&i<o+t?(clearTimeout(l),l=setTimeout((()=>{o=i,e.apply(s,r)}),t)):(o=i,e.apply(s,r))}}function g(e,t){let n;return(...o)=>{clearTimeout(n),n=setTimeout((()=>e.apply(this,o)),t)}}function C(e){try{return e.tocElement||document.querySelector(e.tocSelector)}catch(t){return console.warn(`TOC element not found: ${e.tocSelector}`),null}}const b={_options:d,_buildHtml:r,_parseContent:s,init:u,destroy:f,refresh:m};var S,y;S=("undefined"==typeof global||global instanceof HTMLElement)&&window||global,y=e=>{const n=!!(e&&e.document&&e.document.querySelector&&e.addEventListener);if("undefined"!=typeof window||n)return e.tocbot=t,t},"function"==typeof define&&define.amd?define([],y(S)):"object"!=typeof exports||exports instanceof HTMLElement?S.tocbot=y(S):module.exports=y(S)})();

/* **********************************************
     Begin prism-core.js
********************************************** */

/// <reference lib="WebWorker"/>

var _self = (typeof window !== 'undefined')
	? window   // if in browser
	: (
		(typeof WorkerGlobalScope !== 'undefined' && self instanceof WorkerGlobalScope)
			? self // if in worker
			: {}   // if in node js
	);

/**
 * Prism: Lightweight, robust, elegant syntax highlighting
 *
 * @license MIT <https://opensource.org/licenses/MIT>
 * @author Lea Verou <https://lea.verou.me>
 * @namespace
 * @public
 */
var Prism = (function (_self) {

	// Private helper vars
	var lang = /(?:^|\s)lang(?:uage)?-([\w-]+)(?=\s|$)/i;
	var uniqueId = 0;

	// The grammar object for plaintext
	var plainTextGrammar = {};


	var _ = {
		/**
		 * By default, Prism will attempt to highlight all code elements (by calling {@link Prism.highlightAll}) on the
		 * current page after the page finished loading. This might be a problem if e.g. you wanted to asynchronously load
		 * additional languages or plugins yourself.
		 *
		 * By setting this value to `true`, Prism will not automatically highlight all code elements on the page.
		 *
		 * You obviously have to change this value before the automatic highlighting started. To do this, you can add an
		 * empty Prism object into the global scope before loading the Prism script like this:
		 *
		 * ```js
		 * window.Prism = window.Prism || {};
		 * Prism.manual = true;
		 * // add a new <script> to load Prism's script
		 * ```
		 *
		 * @default false
		 * @type {boolean}
		 * @memberof Prism
		 * @public
		 */
		manual: _self.Prism && _self.Prism.manual,
		/**
		 * By default, if Prism is in a web worker, it assumes that it is in a worker it created itself, so it uses
		 * `addEventListener` to communicate with its parent instance. However, if you're using Prism manually in your
		 * own worker, you don't want it to do this.
		 *
		 * By setting this value to `true`, Prism will not add its own listeners to the worker.
		 *
		 * You obviously have to change this value before Prism executes. To do this, you can add an
		 * empty Prism object into the global scope before loading the Prism script like this:
		 *
		 * ```js
		 * window.Prism = window.Prism || {};
		 * Prism.disableWorkerMessageHandler = true;
		 * // Load Prism's script
		 * ```
		 *
		 * @default false
		 * @type {boolean}
		 * @memberof Prism
		 * @public
		 */
		disableWorkerMessageHandler: _self.Prism && _self.Prism.disableWorkerMessageHandler,

		/**
		 * A namespace for utility methods.
		 *
		 * All function in this namespace that are not explicitly marked as _public_ are for __internal use only__ and may
		 * change or disappear at any time.
		 *
		 * @namespace
		 * @memberof Prism
		 */
		util: {
			encode: function encode(tokens) {
				if (tokens instanceof Token) {
					return new Token(tokens.type, encode(tokens.content), tokens.alias);
				} else if (Array.isArray(tokens)) {
					return tokens.map(encode);
				} else {
					return tokens.replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/\u00a0/g, ' ');
				}
			},

			/**
			 * Returns the name of the type of the given value.
			 *
			 * @param {any} o
			 * @returns {string}
			 * @example
			 * type(null)      === 'Null'
			 * type(undefined) === 'Undefined'
			 * type(123)       === 'Number'
			 * type('foo')     === 'String'
			 * type(true)      === 'Boolean'
			 * type([1, 2])    === 'Array'
			 * type({})        === 'Object'
			 * type(String)    === 'Function'
			 * type(/abc+/)    === 'RegExp'
			 */
			type: function (o) {
				return Object.prototype.toString.call(o).slice(8, -1);
			},

			/**
			 * Returns a unique number for the given object. Later calls will still return the same number.
			 *
			 * @param {Object} obj
			 * @returns {number}
			 */
			objId: function (obj) {
				if (!obj['__id']) {
					Object.defineProperty(obj, '__id', { value: ++uniqueId });
				}
				return obj['__id'];
			},

			/**
			 * Creates a deep clone of the given object.
			 *
			 * The main intended use of this function is to clone language definitions.
			 *
			 * @param {T} o
			 * @param {Record<number, any>} [visited]
			 * @returns {T}
			 * @template T
			 */
			clone: function deepClone(o, visited) {
				visited = visited || {};

				var clone; var id;
				switch (_.util.type(o)) {
					case 'Object':
						id = _.util.objId(o);
						if (visited[id]) {
							return visited[id];
						}
						clone = /** @type {Record<string, any>} */ ({});
						visited[id] = clone;

						for (var key in o) {
							if (o.hasOwnProperty(key)) {
								clone[key] = deepClone(o[key], visited);
							}
						}

						return /** @type {any} */ (clone);

					case 'Array':
						id = _.util.objId(o);
						if (visited[id]) {
							return visited[id];
						}
						clone = [];
						visited[id] = clone;

						(/** @type {Array} */(/** @type {any} */(o))).forEach(function (v, i) {
							clone[i] = deepClone(v, visited);
						});

						return /** @type {any} */ (clone);

					default:
						return o;
				}
			},

			/**
			 * Returns the Prism language of the given element set by a `language-xxxx` or `lang-xxxx` class.
			 *
			 * If no language is set for the element or the element is `null` or `undefined`, `none` will be returned.
			 *
			 * @param {Element} element
			 * @returns {string}
			 */
			getLanguage: function (element) {
				while (element) {
					var m = lang.exec(element.className);
					if (m) {
						return m[1].toLowerCase();
					}
					element = element.parentElement;
				}
				return 'none';
			},

			/**
			 * Sets the Prism `language-xxxx` class of the given element.
			 *
			 * @param {Element} element
			 * @param {string} language
			 * @returns {void}
			 */
			setLanguage: function (element, language) {
				// remove all `language-xxxx` classes
				// (this might leave behind a leading space)
				element.className = element.className.replace(RegExp(lang, 'gi'), '');

				// add the new `language-xxxx` class
				// (using `classList` will automatically clean up spaces for us)
				element.classList.add('language-' + language);
			},

			/**
			 * Returns the script element that is currently executing.
			 *
			 * This does __not__ work for line script element.
			 *
			 * @returns {HTMLScriptElement | null}
			 */
			currentScript: function () {
				if (typeof document === 'undefined') {
					return null;
				}
				if (document.currentScript && document.currentScript.tagName === 'SCRIPT' && 1 < 2 /* hack to trip TS' flow analysis */) {
					return /** @type {any} */ (document.currentScript);
				}

				// IE11 workaround
				// we'll get the src of the current script by parsing IE11's error stack trace
				// this will not work for inline scripts

				try {
					throw new Error();
				} catch (err) {
					// Get file src url from stack. Specifically works with the format of stack traces in IE.
					// A stack will look like this:
					//
					// Error
					//    at _.util.currentScript (http://localhost/components/prism-core.js:119:5)
					//    at Global code (http://localhost/components/prism-core.js:606:1)

					var src = (/at [^(\r\n]*\((.*):[^:]+:[^:]+\)$/i.exec(err.stack) || [])[1];
					if (src) {
						var scripts = document.getElementsByTagName('script');
						for (var i in scripts) {
							if (scripts[i].src == src) {
								return scripts[i];
							}
						}
					}
					return null;
				}
			},

			/**
			 * Returns whether a given class is active for `element`.
			 *
			 * The class can be activated if `element` or one of its ancestors has the given class and it can be deactivated
			 * if `element` or one of its ancestors has the negated version of the given class. The _negated version_ of the
			 * given class is just the given class with a `no-` prefix.
			 *
			 * Whether the class is active is determined by the closest ancestor of `element` (where `element` itself is
			 * closest ancestor) that has the given class or the negated version of it. If neither `element` nor any of its
			 * ancestors have the given class or the negated version of it, then the default activation will be returned.
			 *
			 * In the paradoxical situation where the closest ancestor contains __both__ the given class and the negated
			 * version of it, the class is considered active.
			 *
			 * @param {Element} element
			 * @param {string} className
			 * @param {boolean} [defaultActivation=false]
			 * @returns {boolean}
			 */
			isActive: function (element, className, defaultActivation) {
				var no = 'no-' + className;

				while (element) {
					var classList = element.classList;
					if (classList.contains(className)) {
						return true;
					}
					if (classList.contains(no)) {
						return false;
					}
					element = element.parentElement;
				}
				return !!defaultActivation;
			}
		},

		/**
		 * This namespace contains all currently loaded languages and the some helper functions to create and modify languages.
		 *
		 * @namespace
		 * @memberof Prism
		 * @public
		 */
		languages: {
			/**
			 * The grammar for plain, unformatted text.
			 */
			plain: plainTextGrammar,
			plaintext: plainTextGrammar,
			text: plainTextGrammar,
			txt: plainTextGrammar,

			/**
			 * Creates a deep copy of the language with the given id and appends the given tokens.
			 *
			 * If a token in `redef` also appears in the copied language, then the existing token in the copied language
			 * will be overwritten at its original position.
			 *
			 * ## Best practices
			 *
			 * Since the position of overwriting tokens (token in `redef` that overwrite tokens in the copied language)
			 * doesn't matter, they can technically be in any order. However, this can be confusing to others that trying to
			 * understand the language definition because, normally, the order of tokens matters in Prism grammars.
			 *
			 * Therefore, it is encouraged to order overwriting tokens according to the positions of the overwritten tokens.
			 * Furthermore, all non-overwriting tokens should be placed after the overwriting ones.
			 *
			 * @param {string} id The id of the language to extend. This has to be a key in `Prism.languages`.
			 * @param {Grammar} redef The new tokens to append.
			 * @returns {Grammar} The new language created.
			 * @public
			 * @example
			 * Prism.languages['css-with-colors'] = Prism.languages.extend('css', {
			 *     // Prism.languages.css already has a 'comment' token, so this token will overwrite CSS' 'comment' token
			 *     // at its original position
			 *     'comment': { ... },
			 *     // CSS doesn't have a 'color' token, so this token will be appended
			 *     'color': /\b(?:red|green|blue)\b/
			 * });
			 */
			extend: function (id, redef) {
				var lang = _.util.clone(_.languages[id]);

				for (var key in redef) {
					lang[key] = redef[key];
				}

				return lang;
			},

			/**
			 * Inserts tokens _before_ another token in a language definition or any other grammar.
			 *
			 * ## Usage
			 *
			 * This helper method makes it easy to modify existing languages. For example, the CSS language definition
			 * not only defines CSS highlighting for CSS documents, but also needs to define highlighting for CSS embedded
			 * in HTML through `<style>` elements. To do this, it needs to modify `Prism.languages.markup` and add the
			 * appropriate tokens. However, `Prism.languages.markup` is a regular JavaScript object literal, so if you do
			 * this:
			 *
			 * ```js
			 * Prism.languages.markup.style = {
			 *     // token
			 * };
			 * ```
			 *
			 * then the `style` token will be added (and processed) at the end. `insertBefore` allows you to insert tokens
			 * before existing tokens. For the CSS example above, you would use it like this:
			 *
			 * ```js
			 * Prism.languages.insertBefore('markup', 'cdata', {
			 *     'style': {
			 *         // token
			 *     }
			 * });
			 * ```
			 *
			 * ## Special cases
			 *
			 * If the grammars of `inside` and `insert` have tokens with the same name, the tokens in `inside`'s grammar
			 * will be ignored.
			 *
			 * This behavior can be used to insert tokens after `before`:
			 *
			 * ```js
			 * Prism.languages.insertBefore('markup', 'comment', {
			 *     'comment': Prism.languages.markup.comment,
			 *     // tokens after 'comment'
			 * });
			 * ```
			 *
			 * ## Limitations
			 *
			 * The main problem `insertBefore` has to solve is iteration order. Since ES2015, the iteration order for object
			 * properties is guaranteed to be the insertion order (except for integer keys) but some browsers behave
			 * differently when keys are deleted and re-inserted. So `insertBefore` can't be implemented by temporarily
			 * deleting properties which is necessary to insert at arbitrary positions.
			 *
			 * To solve this problem, `insertBefore` doesn't actually insert the given tokens into the target object.
			 * Instead, it will create a new object and replace all references to the target object with the new one. This
			 * can be done without temporarily deleting properties, so the iteration order is well-defined.
			 *
			 * However, only references that can be reached from `Prism.languages` or `insert` will be replaced. I.e. if
			 * you hold the target object in a variable, then the value of the variable will not change.
			 *
			 * ```js
			 * var oldMarkup = Prism.languages.markup;
			 * var newMarkup = Prism.languages.insertBefore('markup', 'comment', { ... });
			 *
			 * assert(oldMarkup !== Prism.languages.markup);
			 * assert(newMarkup === Prism.languages.markup);
			 * ```
			 *
			 * @param {string} inside The property of `root` (e.g. a language id in `Prism.languages`) that contains the
			 * object to be modified.
			 * @param {string} before The key to insert before.
			 * @param {Grammar} insert An object containing the key-value pairs to be inserted.
			 * @param {Object<string, any>} [root] The object containing `inside`, i.e. the object that contains the
			 * object to be modified.
			 *
			 * Defaults to `Prism.languages`.
			 * @returns {Grammar} The new grammar object.
			 * @public
			 */
			insertBefore: function (inside, before, insert, root) {
				root = root || /** @type {any} */ (_.languages);
				var grammar = root[inside];
				/** @type {Grammar} */
				var ret = {};

				for (var token in grammar) {
					if (grammar.hasOwnProperty(token)) {

						if (token == before) {
							for (var newToken in insert) {
								if (insert.hasOwnProperty(newToken)) {
									ret[newToken] = insert[newToken];
								}
							}
						}

						// Do not insert token which also occur in insert. See #1525
						if (!insert.hasOwnProperty(token)) {
							ret[token] = grammar[token];
						}
					}
				}

				var old = root[inside];
				root[inside] = ret;

				// Update references in other language definitions
				_.languages.DFS(_.languages, function (key, value) {
					if (value === old && key != inside) {
						this[key] = ret;
					}
				});

				return ret;
			},

			// Traverse a language definition with Depth First Search
			DFS: function DFS(o, callback, type, visited) {
				visited = visited || {};

				var objId = _.util.objId;

				for (var i in o) {
					if (o.hasOwnProperty(i)) {
						callback.call(o, i, o[i], type || i);

						var property = o[i];
						var propertyType = _.util.type(property);

						if (propertyType === 'Object' && !visited[objId(property)]) {
							visited[objId(property)] = true;
							DFS(property, callback, null, visited);
						} else if (propertyType === 'Array' && !visited[objId(property)]) {
							visited[objId(property)] = true;
							DFS(property, callback, i, visited);
						}
					}
				}
			}
		},

		plugins: {},

		/**
		 * This is the most high-level function in Prism’s API.
		 * It fetches all the elements that have a `.language-xxxx` class and then calls {@link Prism.highlightElement} on
		 * each one of them.
		 *
		 * This is equivalent to `Prism.highlightAllUnder(document, async, callback)`.
		 *
		 * @param {boolean} [async=false] Same as in {@link Prism.highlightAllUnder}.
		 * @param {HighlightCallback} [callback] Same as in {@link Prism.highlightAllUnder}.
		 * @memberof Prism
		 * @public
		 */
		highlightAll: function (async, callback) {
			_.highlightAllUnder(document, async, callback);
		},

		/**
		 * Fetches all the descendants of `container` that have a `.language-xxxx` class and then calls
		 * {@link Prism.highlightElement} on each one of them.
		 *
		 * The following hooks will be run:
		 * 1. `before-highlightall`
		 * 2. `before-all-elements-highlight`
		 * 3. All hooks of {@link Prism.highlightElement} for each element.
		 *
		 * @param {ParentNode} container The root element, whose descendants that have a `.language-xxxx` class will be highlighted.
		 * @param {boolean} [async=false] Whether each element is to be highlighted asynchronously using Web Workers.
		 * @param {HighlightCallback} [callback] An optional callback to be invoked on each element after its highlighting is done.
		 * @memberof Prism
		 * @public
		 */
		highlightAllUnder: function (container, async, callback) {
			var env = {
				callback: callback,
				container: container,
				selector: 'code[class*="language-"], [class*="language-"] code, code[class*="lang-"], [class*="lang-"] code'
			};

			_.hooks.run('before-highlightall', env);

			env.elements = Array.prototype.slice.apply(env.container.querySelectorAll(env.selector));

			_.hooks.run('before-all-elements-highlight', env);

			for (var i = 0, element; (element = env.elements[i++]);) {
				_.highlightElement(element, async === true, env.callback);
			}
		},

		/**
		 * Highlights the code inside a single element.
		 *
		 * The following hooks will be run:
		 * 1. `before-sanity-check`
		 * 2. `before-highlight`
		 * 3. All hooks of {@link Prism.highlight}. These hooks will be run by an asynchronous worker if `async` is `true`.
		 * 4. `before-insert`
		 * 5. `after-highlight`
		 * 6. `complete`
		 *
		 * Some the above hooks will be skipped if the element doesn't contain any text or there is no grammar loaded for
		 * the element's language.
		 *
		 * @param {Element} element The element containing the code.
		 * It must have a class of `language-xxxx` to be processed, where `xxxx` is a valid language identifier.
		 * @param {boolean} [async=false] Whether the element is to be highlighted asynchronously using Web Workers
		 * to improve performance and avoid blocking the UI when highlighting very large chunks of code. This option is
		 * [disabled by default](https://prismjs.com/faq.html#why-is-asynchronous-highlighting-disabled-by-default).
		 *
		 * Note: All language definitions required to highlight the code must be included in the main `prism.js` file for
		 * asynchronous highlighting to work. You can build your own bundle on the
		 * [Download page](https://prismjs.com/download.html).
		 * @param {HighlightCallback} [callback] An optional callback to be invoked after the highlighting is done.
		 * Mostly useful when `async` is `true`, since in that case, the highlighting is done asynchronously.
		 * @memberof Prism
		 * @public
		 */
		highlightElement: function (element, async, callback) {
			// Find language
			var language = _.util.getLanguage(element);
			var grammar = _.languages[language];

			// Set language on the element, if not present
			_.util.setLanguage(element, language);

			// Set language on the parent, for styling
			var parent = element.parentElement;
			if (parent && parent.nodeName.toLowerCase() === 'pre') {
				_.util.setLanguage(parent, language);
			}

			var code = element.textContent;

			var env = {
				element: element,
				language: language,
				grammar: grammar,
				code: code
			};

			function insertHighlightedCode(highlightedCode) {
				env.highlightedCode = highlightedCode;

				_.hooks.run('before-insert', env);

				env.element.innerHTML = env.highlightedCode;

				_.hooks.run('after-highlight', env);
				_.hooks.run('complete', env);
				callback && callback.call(env.element);
			}

			_.hooks.run('before-sanity-check', env);

			// plugins may change/add the parent/element
			parent = env.element.parentElement;
			if (parent && parent.nodeName.toLowerCase() === 'pre' && !parent.hasAttribute('tabindex')) {
				parent.setAttribute('tabindex', '0');
			}

			if (!env.code) {
				_.hooks.run('complete', env);
				callback && callback.call(env.element);
				return;
			}

			_.hooks.run('before-highlight', env);

			if (!env.grammar) {
				insertHighlightedCode(_.util.encode(env.code));
				return;
			}

			if (async && _self.Worker) {
				var worker = new Worker(_.filename);

				worker.onmessage = function (evt) {
					insertHighlightedCode(evt.data);
				};

				worker.postMessage(JSON.stringify({
					language: env.language,
					code: env.code,
					immediateClose: true
				}));
			} else {
				insertHighlightedCode(_.highlight(env.code, env.grammar, env.language));
			}
		},

		/**
		 * Low-level function, only use if you know what you’re doing. It accepts a string of text as input
		 * and the language definitions to use, and returns a string with the HTML produced.
		 *
		 * The following hooks will be run:
		 * 1. `before-tokenize`
		 * 2. `after-tokenize`
		 * 3. `wrap`: On each {@link Token}.
		 *
		 * @param {string} text A string with the code to be highlighted.
		 * @param {Grammar} grammar An object containing the tokens to use.
		 *
		 * Usually a language definition like `Prism.languages.markup`.
		 * @param {string} language The name of the language definition passed to `grammar`.
		 * @returns {string} The highlighted HTML.
		 * @memberof Prism
		 * @public
		 * @example
		 * Prism.highlight('var foo = true;', Prism.languages.javascript, 'javascript');
		 */
		highlight: function (text, grammar, language) {
			var env = {
				code: text,
				grammar: grammar,
				language: language
			};
			_.hooks.run('before-tokenize', env);
			if (!env.grammar) {
				throw new Error('The language "' + env.language + '" has no grammar.');
			}
			env.tokens = _.tokenize(env.code, env.grammar);
			_.hooks.run('after-tokenize', env);
			return Token.stringify(_.util.encode(env.tokens), env.language);
		},

		/**
		 * This is the heart of Prism, and the most low-level function you can use. It accepts a string of text as input
		 * and the language definitions to use, and returns an array with the tokenized code.
		 *
		 * When the language definition includes nested tokens, the function is called recursively on each of these tokens.
		 *
		 * This method could be useful in other contexts as well, as a very crude parser.
		 *
		 * @param {string} text A string with the code to be highlighted.
		 * @param {Grammar} grammar An object containing the tokens to use.
		 *
		 * Usually a language definition like `Prism.languages.markup`.
		 * @returns {TokenStream} An array of strings and tokens, a token stream.
		 * @memberof Prism
		 * @public
		 * @example
		 * let code = `var foo = 0;`;
		 * let tokens = Prism.tokenize(code, Prism.languages.javascript);
		 * tokens.forEach(token => {
		 *     if (token instanceof Prism.Token && token.type === 'number') {
		 *         console.log(`Found numeric literal: ${token.content}`);
		 *     }
		 * });
		 */
		tokenize: function (text, grammar) {
			var rest = grammar.rest;
			if (rest) {
				for (var token in rest) {
					grammar[token] = rest[token];
				}

				delete grammar.rest;
			}

			var tokenList = new LinkedList();
			addAfter(tokenList, tokenList.head, text);

			matchGrammar(text, tokenList, grammar, tokenList.head, 0);

			return toArray(tokenList);
		},

		/**
		 * @namespace
		 * @memberof Prism
		 * @public
		 */
		hooks: {
			all: {},

			/**
			 * Adds the given callback to the list of callbacks for the given hook.
			 *
			 * The callback will be invoked when the hook it is registered for is run.
			 * Hooks are usually directly run by a highlight function but you can also run hooks yourself.
			 *
			 * One callback function can be registered to multiple hooks and the same hook multiple times.
			 *
			 * @param {string} name The name of the hook.
			 * @param {HookCallback} callback The callback function which is given environment variables.
			 * @public
			 */
			add: function (name, callback) {
				var hooks = _.hooks.all;

				hooks[name] = hooks[name] || [];

				hooks[name].push(callback);
			},

			/**
			 * Runs a hook invoking all registered callbacks with the given environment variables.
			 *
			 * Callbacks will be invoked synchronously and in the order in which they were registered.
			 *
			 * @param {string} name The name of the hook.
			 * @param {Object<string, any>} env The environment variables of the hook passed to all callbacks registered.
			 * @public
			 */
			run: function (name, env) {
				var callbacks = _.hooks.all[name];

				if (!callbacks || !callbacks.length) {
					return;
				}

				for (var i = 0, callback; (callback = callbacks[i++]);) {
					callback(env);
				}
			}
		},

		Token: Token
	};
	_self.Prism = _;


	// Typescript note:
	// The following can be used to import the Token type in JSDoc:
	//
	//   @typedef {InstanceType<import("./prism-core")["Token"]>} Token

	/**
	 * Creates a new token.
	 *
	 * @param {string} type See {@link Token#type type}
	 * @param {string | TokenStream} content See {@link Token#content content}
	 * @param {string|string[]} [alias] The alias(es) of the token.
	 * @param {string} [matchedStr=""] A copy of the full string this token was created from.
	 * @class
	 * @global
	 * @public
	 */
	function Token(type, content, alias, matchedStr) {
		/**
		 * The type of the token.
		 *
		 * This is usually the key of a pattern in a {@link Grammar}.
		 *
		 * @type {string}
		 * @see GrammarToken
		 * @public
		 */
		this.type = type;
		/**
		 * The strings or tokens contained by this token.
		 *
		 * This will be a token stream if the pattern matched also defined an `inside` grammar.
		 *
		 * @type {string | TokenStream}
		 * @public
		 */
		this.content = content;
		/**
		 * The alias(es) of the token.
		 *
		 * @type {string|string[]}
		 * @see GrammarToken
		 * @public
		 */
		this.alias = alias;
		// Copy of the full string this token was created from
		this.length = (matchedStr || '').length | 0;
	}

	/**
	 * A token stream is an array of strings and {@link Token Token} objects.
	 *
	 * Token streams have to fulfill a few properties that are assumed by most functions (mostly internal ones) that process
	 * them.
	 *
	 * 1. No adjacent strings.
	 * 2. No empty strings.
	 *
	 *    The only exception here is the token stream that only contains the empty string and nothing else.
	 *
	 * @typedef {Array<string | Token>} TokenStream
	 * @global
	 * @public
	 */

	/**
	 * Converts the given token or token stream to an HTML representation.
	 *
	 * The following hooks will be run:
	 * 1. `wrap`: On each {@link Token}.
	 *
	 * @param {string | Token | TokenStream} o The token or token stream to be converted.
	 * @param {string} language The name of current language.
	 * @returns {string} The HTML representation of the token or token stream.
	 * @memberof Token
	 * @static
	 */
	Token.stringify = function stringify(o, language) {
		if (typeof o == 'string') {
			return o;
		}
		if (Array.isArray(o)) {
			var s = '';
			o.forEach(function (e) {
				s += stringify(e, language);
			});
			return s;
		}

		var env = {
			type: o.type,
			content: stringify(o.content, language),
			tag: 'span',
			classes: ['token', o.type],
			attributes: {},
			language: language
		};

		var aliases = o.alias;
		if (aliases) {
			if (Array.isArray(aliases)) {
				Array.prototype.push.apply(env.classes, aliases);
			} else {
				env.classes.push(aliases);
			}
		}

		_.hooks.run('wrap', env);

		var attributes = '';
		for (var name in env.attributes) {
			attributes += ' ' + name + '="' + (env.attributes[name] || '').replace(/"/g, '&quot;') + '"';
		}

		return '<' + env.tag + ' class="' + env.classes.join(' ') + '"' + attributes + '>' + env.content + '</' + env.tag + '>';
	};

	/**
	 * @param {RegExp} pattern
	 * @param {number} pos
	 * @param {string} text
	 * @param {boolean} lookbehind
	 * @returns {RegExpExecArray | null}
	 */
	function matchPattern(pattern, pos, text, lookbehind) {
		pattern.lastIndex = pos;
		var match = pattern.exec(text);
		if (match && lookbehind && match[1]) {
			// change the match to remove the text matched by the Prism lookbehind group
			var lookbehindLength = match[1].length;
			match.index += lookbehindLength;
			match[0] = match[0].slice(lookbehindLength);
		}
		return match;
	}

	/**
	 * @param {string} text
	 * @param {LinkedList<string | Token>} tokenList
	 * @param {any} grammar
	 * @param {LinkedListNode<string | Token>} startNode
	 * @param {number} startPos
	 * @param {RematchOptions} [rematch]
	 * @returns {void}
	 * @private
	 *
	 * @typedef RematchOptions
	 * @property {string} cause
	 * @property {number} reach
	 */
	function matchGrammar(text, tokenList, grammar, startNode, startPos, rematch) {
		for (var token in grammar) {
			if (!grammar.hasOwnProperty(token) || !grammar[token]) {
				continue;
			}

			var patterns = grammar[token];
			patterns = Array.isArray(patterns) ? patterns : [patterns];

			for (var j = 0; j < patterns.length; ++j) {
				if (rematch && rematch.cause == token + ',' + j) {
					return;
				}

				var patternObj = patterns[j];
				var inside = patternObj.inside;
				var lookbehind = !!patternObj.lookbehind;
				var greedy = !!patternObj.greedy;
				var alias = patternObj.alias;

				if (greedy && !patternObj.pattern.global) {
					// Without the global flag, lastIndex won't work
					var flags = patternObj.pattern.toString().match(/[imsuy]*$/)[0];
					patternObj.pattern = RegExp(patternObj.pattern.source, flags + 'g');
				}

				/** @type {RegExp} */
				var pattern = patternObj.pattern || patternObj;

				for ( // iterate the token list and keep track of the current token/string position
					var currentNode = startNode.next, pos = startPos;
					currentNode !== tokenList.tail;
					pos += currentNode.value.length, currentNode = currentNode.next
				) {

					if (rematch && pos >= rematch.reach) {
						break;
					}

					var str = currentNode.value;

					if (tokenList.length > text.length) {
						// Something went terribly wrong, ABORT, ABORT!
						return;
					}

					if (str instanceof Token) {
						continue;
					}

					var removeCount = 1; // this is the to parameter of removeBetween
					var match;

					if (greedy) {
						match = matchPattern(pattern, pos, text, lookbehind);
						if (!match || match.index >= text.length) {
							break;
						}

						var from = match.index;
						var to = match.index + match[0].length;
						var p = pos;

						// find the node that contains the match
						p += currentNode.value.length;
						while (from >= p) {
							currentNode = currentNode.next;
							p += currentNode.value.length;
						}
						// adjust pos (and p)
						p -= currentNode.value.length;
						pos = p;

						// the current node is a Token, then the match starts inside another Token, which is invalid
						if (currentNode.value instanceof Token) {
							continue;
						}

						// find the last node which is affected by this match
						for (
							var k = currentNode;
							k !== tokenList.tail && (p < to || typeof k.value === 'string');
							k = k.next
						) {
							removeCount++;
							p += k.value.length;
						}
						removeCount--;

						// replace with the new match
						str = text.slice(pos, p);
						match.index -= pos;
					} else {
						match = matchPattern(pattern, 0, str, lookbehind);
						if (!match) {
							continue;
						}
					}

					// eslint-disable-next-line no-redeclare
					var from = match.index;
					var matchStr = match[0];
					var before = str.slice(0, from);
					var after = str.slice(from + matchStr.length);

					var reach = pos + str.length;
					if (rematch && reach > rematch.reach) {
						rematch.reach = reach;
					}

					var removeFrom = currentNode.prev;

					if (before) {
						removeFrom = addAfter(tokenList, removeFrom, before);
						pos += before.length;
					}

					removeRange(tokenList, removeFrom, removeCount);

					var wrapped = new Token(token, inside ? _.tokenize(matchStr, inside) : matchStr, alias, matchStr);
					currentNode = addAfter(tokenList, removeFrom, wrapped);

					if (after) {
						addAfter(tokenList, currentNode, after);
					}

					if (removeCount > 1) {
						// at least one Token object was removed, so we have to do some rematching
						// this can only happen if the current pattern is greedy

						/** @type {RematchOptions} */
						var nestedRematch = {
							cause: token + ',' + j,
							reach: reach
						};
						matchGrammar(text, tokenList, grammar, currentNode.prev, pos, nestedRematch);

						// the reach might have been extended because of the rematching
						if (rematch && nestedRematch.reach > rematch.reach) {
							rematch.reach = nestedRematch.reach;
						}
					}
				}
			}
		}
	}

	/**
	 * @typedef LinkedListNode
	 * @property {T} value
	 * @property {LinkedListNode<T> | null} prev The previous node.
	 * @property {LinkedListNode<T> | null} next The next node.
	 * @template T
	 * @private
	 */

	/**
	 * @template T
	 * @private
	 */
	function LinkedList() {
		/** @type {LinkedListNode<T>} */
		var head = { value: null, prev: null, next: null };
		/** @type {LinkedListNode<T>} */
		var tail = { value: null, prev: head, next: null };
		head.next = tail;

		/** @type {LinkedListNode<T>} */
		this.head = head;
		/** @type {LinkedListNode<T>} */
		this.tail = tail;
		this.length = 0;
	}

	/**
	 * Adds a new node with the given value to the list.
	 *
	 * @param {LinkedList<T>} list
	 * @param {LinkedListNode<T>} node
	 * @param {T} value
	 * @returns {LinkedListNode<T>} The added node.
	 * @template T
	 */
	function addAfter(list, node, value) {
		// assumes that node != list.tail && values.length >= 0
		var next = node.next;

		var newNode = { value: value, prev: node, next: next };
		node.next = newNode;
		next.prev = newNode;
		list.length++;

		return newNode;
	}
	/**
	 * Removes `count` nodes after the given node. The given node will not be removed.
	 *
	 * @param {LinkedList<T>} list
	 * @param {LinkedListNode<T>} node
	 * @param {number} count
	 * @template T
	 */
	function removeRange(list, node, count) {
		var next = node.next;
		for (var i = 0; i < count && next !== list.tail; i++) {
			next = next.next;
		}
		node.next = next;
		next.prev = node;
		list.length -= i;
	}
	/**
	 * @param {LinkedList<T>} list
	 * @returns {T[]}
	 * @template T
	 */
	function toArray(list) {
		var array = [];
		var node = list.head.next;
		while (node !== list.tail) {
			array.push(node.value);
			node = node.next;
		}
		return array;
	}


	if (!_self.document) {
		if (!_self.addEventListener) {
			// in Node.js
			return _;
		}

		if (!_.disableWorkerMessageHandler) {
			// In worker
			_self.addEventListener('message', function (evt) {
				var message = JSON.parse(evt.data);
				var lang = message.language;
				var code = message.code;
				var immediateClose = message.immediateClose;

				_self.postMessage(_.highlight(code, _.languages[lang], lang));
				if (immediateClose) {
					_self.close();
				}
			}, false);
		}

		return _;
	}

	// Get current script and highlight
	var script = _.util.currentScript();

	if (script) {
		_.filename = script.src;

		if (script.hasAttribute('data-manual')) {
			_.manual = true;
		}
	}

	function highlightAutomaticallyCallback() {
		if (!_.manual) {
			_.highlightAll();
		}
	}

	if (!_.manual) {
		// If the document state is "loading", then we'll use DOMContentLoaded.
		// If the document state is "interactive" and the prism.js script is deferred, then we'll also use the
		// DOMContentLoaded event because there might be some plugins or languages which have also been deferred and they
		// might take longer one animation frame to execute which can create a race condition where only some plugins have
		// been loaded when Prism.highlightAll() is executed, depending on how fast resources are loaded.
		// See https://github.com/PrismJS/prism/issues/2102
		var readyState = document.readyState;
		if (readyState === 'loading' || readyState === 'interactive' && script && script.defer) {
			document.addEventListener('DOMContentLoaded', highlightAutomaticallyCallback);
		} else {
			if (window.requestAnimationFrame) {
				window.requestAnimationFrame(highlightAutomaticallyCallback);
			} else {
				window.setTimeout(highlightAutomaticallyCallback, 16);
			}
		}
	}

	return _;

}(_self));

if (typeof module !== 'undefined' && module.exports) {
	module.exports = Prism;
}

// hack for components to work correctly in node.js
if (typeof global !== 'undefined') {
	global.Prism = Prism;
}

// some additional documentation/types

/**
 * The expansion of a simple `RegExp` literal to support additional properties.
 *
 * @typedef GrammarToken
 * @property {RegExp} pattern The regular expression of the token.
 * @property {boolean} [lookbehind=false] If `true`, then the first capturing group of `pattern` will (effectively)
 * behave as a lookbehind group meaning that the captured text will not be part of the matched text of the new token.
 * @property {boolean} [greedy=false] Whether the token is greedy.
 * @property {string|string[]} [alias] An optional alias or list of aliases.
 * @property {Grammar} [inside] The nested grammar of this token.
 *
 * The `inside` grammar will be used to tokenize the text value of each token of this kind.
 *
 * This can be used to make nested and even recursive language definitions.
 *
 * Note: This can cause infinite recursion. Be careful when you embed different languages or even the same language into
 * each another.
 * @global
 * @public
 */

/**
 * @typedef Grammar
 * @type {Object<string, RegExp | GrammarToken | Array<RegExp | GrammarToken>>}
 * @property {Grammar} [rest] An optional grammar object that will be appended to this grammar.
 * @global
 * @public
 */

/**
 * A function which will invoked after an element was successfully highlighted.
 *
 * @callback HighlightCallback
 * @param {Element} element The element successfully highlighted.
 * @returns {void}
 * @global
 * @public
 */

/**
 * @callback HookCallback
 * @param {Object<string, any>} env The environment variables of the hook.
 * @returns {void}
 * @global
 * @public
 */


/* **********************************************
     Begin prism-markup.js
********************************************** */

Prism.languages.markup = {
	'comment': {
		pattern: /<!--(?:(?!<!--)[\s\S])*?-->/,
		greedy: true
	},
	'prolog': {
		pattern: /<\?[\s\S]+?\?>/,
		greedy: true
	},
	'doctype': {
		// https://www.w3.org/TR/xml/#NT-doctypedecl
		pattern: /<!DOCTYPE(?:[^>"'[\]]|"[^"]*"|'[^']*')+(?:\[(?:[^<"'\]]|"[^"]*"|'[^']*'|<(?!!--)|<!--(?:[^-]|-(?!->))*-->)*\]\s*)?>/i,
		greedy: true,
		inside: {
			'internal-subset': {
				pattern: /(^[^\[]*\[)[\s\S]+(?=\]>$)/,
				lookbehind: true,
				greedy: true,
				inside: null // see below
			},
			'string': {
				pattern: /"[^"]*"|'[^']*'/,
				greedy: true
			},
			'punctuation': /^<!|>$|[[\]]/,
			'doctype-tag': /^DOCTYPE/i,
			'name': /[^\s<>'"]+/
		}
	},
	'cdata': {
		pattern: /<!\[CDATA\[[\s\S]*?\]\]>/i,
		greedy: true
	},
	'tag': {
		pattern: /<\/?(?!\d)[^\s>\/=$<%]+(?:\s(?:\s*[^\s>\/=]+(?:\s*=\s*(?:"[^"]*"|'[^']*'|[^\s'">=]+(?=[\s>]))|(?=[\s/>])))+)?\s*\/?>/,
		greedy: true,
		inside: {
			'tag': {
				pattern: /^<\/?[^\s>\/]+/,
				inside: {
					'punctuation': /^<\/?/,
					'namespace': /^[^\s>\/:]+:/
				}
			},
			'special-attr': [],
			'attr-value': {
				pattern: /=\s*(?:"[^"]*"|'[^']*'|[^\s'">=]+)/,
				inside: {
					'punctuation': [
						{
							pattern: /^=/,
							alias: 'attr-equals'
						},
						{
							pattern: /^(\s*)["']|["']$/,
							lookbehind: true
						}
					]
				}
			},
			'punctuation': /\/?>/,
			'attr-name': {
				pattern: /[^\s>\/]+/,
				inside: {
					'namespace': /^[^\s>\/:]+:/
				}
			}

		}
	},
	'entity': [
		{
			pattern: /&[\da-z]{1,8};/i,
			alias: 'named-entity'
		},
		/&#x?[\da-f]{1,8};/i
	]
};

Prism.languages.markup['tag'].inside['attr-value'].inside['entity'] =
	Prism.languages.markup['entity'];
Prism.languages.markup['doctype'].inside['internal-subset'].inside = Prism.languages.markup;

// Plugin to make entity title show the real entity, idea by Roman Komarov
Prism.hooks.add('wrap', function (env) {

	if (env.type === 'entity') {
		env.attributes['title'] = env.content.replace(/&amp;/, '&');
	}
});

Object.defineProperty(Prism.languages.markup.tag, 'addInlined', {
	/**
	 * Adds an inlined language to markup.
	 *
	 * An example of an inlined language is CSS with `<style>` tags.
	 *
	 * @param {string} tagName The name of the tag that contains the inlined language. This name will be treated as
	 * case insensitive.
	 * @param {string} lang The language key.
	 * @example
	 * addInlined('style', 'css');
	 */
	value: function addInlined(tagName, lang) {
		var includedCdataInside = {};
		includedCdataInside['language-' + lang] = {
			pattern: /(^<!\[CDATA\[)[\s\S]+?(?=\]\]>$)/i,
			lookbehind: true,
			inside: Prism.languages[lang]
		};
		includedCdataInside['cdata'] = /^<!\[CDATA\[|\]\]>$/i;

		var inside = {
			'included-cdata': {
				pattern: /<!\[CDATA\[[\s\S]*?\]\]>/i,
				inside: includedCdataInside
			}
		};
		inside['language-' + lang] = {
			pattern: /[\s\S]+/,
			inside: Prism.languages[lang]
		};

		var def = {};
		def[tagName] = {
			pattern: RegExp(/(<__[^>]*>)(?:<!\[CDATA\[(?:[^\]]|\](?!\]>))*\]\]>|(?!<!\[CDATA\[)[\s\S])*?(?=<\/__>)/.source.replace(/__/g, function () { return tagName; }), 'i'),
			lookbehind: true,
			greedy: true,
			inside: inside
		};

		Prism.languages.insertBefore('markup', 'cdata', def);
	}
});
Object.defineProperty(Prism.languages.markup.tag, 'addAttribute', {
	/**
	 * Adds an pattern to highlight languages embedded in HTML attributes.
	 *
	 * An example of an inlined language is CSS with `style` attributes.
	 *
	 * @param {string} attrName The name of the tag that contains the inlined language. This name will be treated as
	 * case insensitive.
	 * @param {string} lang The language key.
	 * @example
	 * addAttribute('style', 'css');
	 */
	value: function (attrName, lang) {
		Prism.languages.markup.tag.inside['special-attr'].push({
			pattern: RegExp(
				/(^|["'\s])/.source + '(?:' + attrName + ')' + /\s*=\s*(?:"[^"]*"|'[^']*'|[^\s'">=]+(?=[\s>]))/.source,
				'i'
			),
			lookbehind: true,
			inside: {
				'attr-name': /^[^\s=]+/,
				'attr-value': {
					pattern: /=[\s\S]+/,
					inside: {
						'value': {
							pattern: /(^=\s*(["']|(?!["'])))\S[\s\S]*(?=\2$)/,
							lookbehind: true,
							alias: [lang, 'language-' + lang],
							inside: Prism.languages[lang]
						},
						'punctuation': [
							{
								pattern: /^=/,
								alias: 'attr-equals'
							},
							/"|'/
						]
					}
				}
			}
		});
	}
});

Prism.languages.html = Prism.languages.markup;
Prism.languages.mathml = Prism.languages.markup;
Prism.languages.svg = Prism.languages.markup;

Prism.languages.xml = Prism.languages.extend('markup', {});
Prism.languages.ssml = Prism.languages.xml;
Prism.languages.atom = Prism.languages.xml;
Prism.languages.rss = Prism.languages.xml;


/* **********************************************
     Begin prism-css.js
********************************************** */

(function (Prism) {

	var string = /(?:"(?:\\(?:\r\n|[\s\S])|[^"\\\r\n])*"|'(?:\\(?:\r\n|[\s\S])|[^'\\\r\n])*')/;

	Prism.languages.css = {
		'comment': /\/\*[\s\S]*?\*\//,
		'atrule': {
			pattern: RegExp('@[\\w-](?:' + /[^;{\s"']|\s+(?!\s)/.source + '|' + string.source + ')*?' + /(?:;|(?=\s*\{))/.source),
			inside: {
				'rule': /^@[\w-]+/,
				'selector-function-argument': {
					pattern: /(\bselector\s*\(\s*(?![\s)]))(?:[^()\s]|\s+(?![\s)])|\((?:[^()]|\([^()]*\))*\))+(?=\s*\))/,
					lookbehind: true,
					alias: 'selector'
				},
				'keyword': {
					pattern: /(^|[^\w-])(?:and|not|only|or)(?![\w-])/,
					lookbehind: true
				}
				// See rest below
			}
		},
		'url': {
			// https://drafts.csswg.org/css-values-3/#urls
			pattern: RegExp('\\burl\\((?:' + string.source + '|' + /(?:[^\\\r\n()"']|\\[\s\S])*/.source + ')\\)', 'i'),
			greedy: true,
			inside: {
				'function': /^url/i,
				'punctuation': /^\(|\)$/,
				'string': {
					pattern: RegExp('^' + string.source + '$'),
					alias: 'url'
				}
			}
		},
		'selector': {
			pattern: RegExp('(^|[{}\\s])[^{}\\s](?:[^{};"\'\\s]|\\s+(?![\\s{])|' + string.source + ')*(?=\\s*\\{)'),
			lookbehind: true
		},
		'string': {
			pattern: string,
			greedy: true
		},
		'property': {
			pattern: /(^|[^-\w\xA0-\uFFFF])(?!\s)[-_a-z\xA0-\uFFFF](?:(?!\s)[-\w\xA0-\uFFFF])*(?=\s*:)/i,
			lookbehind: true
		},
		'important': /!important\b/i,
		'function': {
			pattern: /(^|[^-a-z0-9])[-a-z0-9]+(?=\()/i,
			lookbehind: true
		},
		'punctuation': /[(){};:,]/
	};

	Prism.languages.css['atrule'].inside.rest = Prism.languages.css;

	var markup = Prism.languages.markup;
	if (markup) {
		markup.tag.addInlined('style', 'css');
		markup.tag.addAttribute('style', 'css');
	}

}(Prism));


/* **********************************************
     Begin prism-clike.js
********************************************** */

Prism.languages.clike = {
	'comment': [
		{
			pattern: /(^|[^\\])\/\*[\s\S]*?(?:\*\/|$)/,
			lookbehind: true,
			greedy: true
		},
		{
			pattern: /(^|[^\\:])\/\/.*/,
			lookbehind: true,
			greedy: true
		}
	],
	'string': {
		pattern: /(["'])(?:\\(?:\r\n|[\s\S])|(?!\1)[^\\\r\n])*\1/,
		greedy: true
	},
	'class-name': {
		pattern: /(\b(?:class|extends|implements|instanceof|interface|new|trait)\s+|\bcatch\s+\()[\w.\\]+/i,
		lookbehind: true,
		inside: {
			'punctuation': /[.\\]/
		}
	},
	'keyword': /\b(?:break|catch|continue|do|else|finally|for|function|if|in|instanceof|new|null|return|throw|try|while)\b/,
	'boolean': /\b(?:false|true)\b/,
	'function': /\b\w+(?=\()/,
	'number': /\b0x[\da-f]+\b|(?:\b\d+(?:\.\d*)?|\B\.\d+)(?:e[+-]?\d+)?/i,
	'operator': /[<>]=?|[!=]=?=?|--?|\+\+?|&&?|\|\|?|[?*/~^%]/,
	'punctuation': /[{}[\];(),.:]/
};


/* **********************************************
     Begin prism-javascript.js
********************************************** */

Prism.languages.javascript = Prism.languages.extend('clike', {
	'class-name': [
		Prism.languages.clike['class-name'],
		{
			pattern: /(^|[^$\w\xA0-\uFFFF])(?!\s)[_$A-Z\xA0-\uFFFF](?:(?!\s)[$\w\xA0-\uFFFF])*(?=\.(?:constructor|prototype))/,
			lookbehind: true
		}
	],
	'keyword': [
		{
			pattern: /((?:^|\})\s*)catch\b/,
			lookbehind: true
		},
		{
			pattern: /(^|[^.]|\.\.\.\s*)\b(?:as|assert(?=\s*\{)|async(?=\s*(?:function\b|\(|[$\w\xA0-\uFFFF]|$))|await|break|case|class|const|continue|debugger|default|delete|do|else|enum|export|extends|finally(?=\s*(?:\{|$))|for|from(?=\s*(?:['"]|$))|function|(?:get|set)(?=\s*(?:[#\[$\w\xA0-\uFFFF]|$))|if|implements|import|in|instanceof|interface|let|new|null|of|package|private|protected|public|return|static|super|switch|this|throw|try|typeof|undefined|var|void|while|with|yield)\b/,
			lookbehind: true
		},
	],
	// Allow for all non-ASCII characters (See http://stackoverflow.com/a/2008444)
	'function': /#?(?!\s)[_$a-zA-Z\xA0-\uFFFF](?:(?!\s)[$\w\xA0-\uFFFF])*(?=\s*(?:\.\s*(?:apply|bind|call)\s*)?\()/,
	'number': {
		pattern: RegExp(
			/(^|[^\w$])/.source +
			'(?:' +
			(
				// constant
				/NaN|Infinity/.source +
				'|' +
				// binary integer
				/0[bB][01]+(?:_[01]+)*n?/.source +
				'|' +
				// octal integer
				/0[oO][0-7]+(?:_[0-7]+)*n?/.source +
				'|' +
				// hexadecimal integer
				/0[xX][\dA-Fa-f]+(?:_[\dA-Fa-f]+)*n?/.source +
				'|' +
				// decimal bigint
				/\d+(?:_\d+)*n/.source +
				'|' +
				// decimal number (integer or float) but no bigint
				/(?:\d+(?:_\d+)*(?:\.(?:\d+(?:_\d+)*)?)?|\.\d+(?:_\d+)*)(?:[Ee][+-]?\d+(?:_\d+)*)?/.source
			) +
			')' +
			/(?![\w$])/.source
		),
		lookbehind: true
	},
	'operator': /--|\+\+|\*\*=?|=>|&&=?|\|\|=?|[!=]==|<<=?|>>>?=?|[-+*/%&|^!=<>]=?|\.{3}|\?\?=?|\?\.?|[~:]/
});

Prism.languages.javascript['class-name'][0].pattern = /(\b(?:class|extends|implements|instanceof|interface|new)\s+)[\w.\\]+/;

Prism.languages.insertBefore('javascript', 'keyword', {
	'regex': {
		pattern: RegExp(
			// lookbehind
			// eslint-disable-next-line regexp/no-dupe-characters-character-class
			/((?:^|[^$\w\xA0-\uFFFF."'\])\s]|\b(?:return|yield))\s*)/.source +
			// Regex pattern:
			// There are 2 regex patterns here. The RegExp set notation proposal added support for nested character
			// classes if the `v` flag is present. Unfortunately, nested CCs are both context-free and incompatible
			// with the only syntax, so we have to define 2 different regex patterns.
			/\//.source +
			'(?:' +
			/(?:\[(?:[^\]\\\r\n]|\\.)*\]|\\.|[^/\\\[\r\n])+\/[dgimyus]{0,7}/.source +
			'|' +
			// `v` flag syntax. This supports 3 levels of nested character classes.
			/(?:\[(?:[^[\]\\\r\n]|\\.|\[(?:[^[\]\\\r\n]|\\.|\[(?:[^[\]\\\r\n]|\\.)*\])*\])*\]|\\.|[^/\\\[\r\n])+\/[dgimyus]{0,7}v[dgimyus]{0,7}/.source +
			')' +
			// lookahead
			/(?=(?:\s|\/\*(?:[^*]|\*(?!\/))*\*\/)*(?:$|[\r\n,.;:})\]]|\/\/))/.source
		),
		lookbehind: true,
		greedy: true,
		inside: {
			'regex-source': {
				pattern: /^(\/)[\s\S]+(?=\/[a-z]*$)/,
				lookbehind: true,
				alias: 'language-regex',
				inside: Prism.languages.regex
			},
			'regex-delimiter': /^\/|\/$/,
			'regex-flags': /^[a-z]+$/,
		}
	},
	// This must be declared before keyword because we use "function" inside the look-forward
	'function-variable': {
		pattern: /#?(?!\s)[_$a-zA-Z\xA0-\uFFFF](?:(?!\s)[$\w\xA0-\uFFFF])*(?=\s*[=:]\s*(?:async\s*)?(?:\bfunction\b|(?:\((?:[^()]|\([^()]*\))*\)|(?!\s)[_$a-zA-Z\xA0-\uFFFF](?:(?!\s)[$\w\xA0-\uFFFF])*)\s*=>))/,
		alias: 'function'
	},
	'parameter': [
		{
			pattern: /(function(?:\s+(?!\s)[_$a-zA-Z\xA0-\uFFFF](?:(?!\s)[$\w\xA0-\uFFFF])*)?\s*\(\s*)(?!\s)(?:[^()\s]|\s+(?![\s)])|\([^()]*\))+(?=\s*\))/,
			lookbehind: true,
			inside: Prism.languages.javascript
		},
		{
			pattern: /(^|[^$\w\xA0-\uFFFF])(?!\s)[_$a-z\xA0-\uFFFF](?:(?!\s)[$\w\xA0-\uFFFF])*(?=\s*=>)/i,
			lookbehind: true,
			inside: Prism.languages.javascript
		},
		{
			pattern: /(\(\s*)(?!\s)(?:[^()\s]|\s+(?![\s)])|\([^()]*\))+(?=\s*\)\s*=>)/,
			lookbehind: true,
			inside: Prism.languages.javascript
		},
		{
			pattern: /((?:\b|\s|^)(?!(?:as|async|await|break|case|catch|class|const|continue|debugger|default|delete|do|else|enum|export|extends|finally|for|from|function|get|if|implements|import|in|instanceof|interface|let|new|null|of|package|private|protected|public|return|set|static|super|switch|this|throw|try|typeof|undefined|var|void|while|with|yield)(?![$\w\xA0-\uFFFF]))(?:(?!\s)[_$a-zA-Z\xA0-\uFFFF](?:(?!\s)[$\w\xA0-\uFFFF])*\s*)\(\s*|\]\s*\(\s*)(?!\s)(?:[^()\s]|\s+(?![\s)])|\([^()]*\))+(?=\s*\)\s*\{)/,
			lookbehind: true,
			inside: Prism.languages.javascript
		}
	],
	'constant': /\b[A-Z](?:[A-Z_]|\dx?)*\b/
});

Prism.languages.insertBefore('javascript', 'string', {
	'hashbang': {
		pattern: /^#!.*/,
		greedy: true,
		alias: 'comment'
	},
	'template-string': {
		pattern: /`(?:\\[\s\S]|\$\{(?:[^{}]|\{(?:[^{}]|\{[^}]*\})*\})+\}|(?!\$\{)[^\\`])*`/,
		greedy: true,
		inside: {
			'template-punctuation': {
				pattern: /^`|`$/,
				alias: 'string'
			},
			'interpolation': {
				pattern: /((?:^|[^\\])(?:\\{2})*)\$\{(?:[^{}]|\{(?:[^{}]|\{[^}]*\})*\})+\}/,
				lookbehind: true,
				inside: {
					'interpolation-punctuation': {
						pattern: /^\$\{|\}$/,
						alias: 'punctuation'
					},
					rest: Prism.languages.javascript
				}
			},
			'string': /[\s\S]+/
		}
	},
	'string-property': {
		pattern: /((?:^|[,{])[ \t]*)(["'])(?:\\(?:\r\n|[\s\S])|(?!\2)[^\\\r\n])*\2(?=\s*:)/m,
		lookbehind: true,
		greedy: true,
		alias: 'property'
	}
});

Prism.languages.insertBefore('javascript', 'operator', {
	'literal-property': {
		pattern: /((?:^|[,{])[ \t]*)(?!\s)[_$a-zA-Z\xA0-\uFFFF](?:(?!\s)[$\w\xA0-\uFFFF])*(?=\s*:)/m,
		lookbehind: true,
		alias: 'property'
	},
});

if (Prism.languages.markup) {
	Prism.languages.markup.tag.addInlined('script', 'javascript');

	// add attribute support for all DOM events.
	// https://developer.mozilla.org/en-US/docs/Web/Events#Standard_events
	Prism.languages.markup.tag.addAttribute(
		/on(?:abort|blur|change|click|composition(?:end|start|update)|dblclick|error|focus(?:in|out)?|key(?:down|up)|load|mouse(?:down|enter|leave|move|out|over|up)|reset|resize|scroll|select|slotchange|submit|unload|wheel)/.source,
		'javascript'
	);
}

Prism.languages.js = Prism.languages.javascript;


/* **********************************************
     Begin prism-file-highlight.js
********************************************** */

(function () {

	if (typeof Prism === 'undefined' || typeof document === 'undefined') {
		return;
	}

	// https://developer.mozilla.org/en-US/docs/Web/API/Element/matches#Polyfill
	if (!Element.prototype.matches) {
		Element.prototype.matches = Element.prototype.msMatchesSelector || Element.prototype.webkitMatchesSelector;
	}

	var LOADING_MESSAGE = 'Loading…';
	var FAILURE_MESSAGE = function (status, message) {
		return '✖ Error ' + status + ' while fetching file: ' + message;
	};
	var FAILURE_EMPTY_MESSAGE = '✖ Error: File does not exist or is empty';

	var EXTENSIONS = {
		'js': 'javascript',
		'py': 'python',
		'rb': 'ruby',
		'ps1': 'powershell',
		'psm1': 'powershell',
		'sh': 'bash',
		'bat': 'batch',
		'h': 'c',
		'tex': 'latex'
	};

	var STATUS_ATTR = 'data-src-status';
	var STATUS_LOADING = 'loading';
	var STATUS_LOADED = 'loaded';
	var STATUS_FAILED = 'failed';

	var SELECTOR = 'pre[data-src]:not([' + STATUS_ATTR + '="' + STATUS_LOADED + '"])'
		+ ':not([' + STATUS_ATTR + '="' + STATUS_LOADING + '"])';

	/**
	 * Loads the given file.
	 *
	 * @param {string} src The URL or path of the source file to load.
	 * @param {(result: string) => void} success
	 * @param {(reason: string) => void} error
	 */
	function loadFile(src, success, error) {
		var xhr = new XMLHttpRequest();
		xhr.open('GET', src, true);
		xhr.onreadystatechange = function () {
			if (xhr.readyState == 4) {
				if (xhr.status < 400 && xhr.responseText) {
					success(xhr.responseText);
				} else {
					if (xhr.status >= 400) {
						error(FAILURE_MESSAGE(xhr.status, xhr.statusText));
					} else {
						error(FAILURE_EMPTY_MESSAGE);
					}
				}
			}
		};
		xhr.send(null);
	}

	/**
	 * Parses the given range.
	 *
	 * This returns a range with inclusive ends.
	 *
	 * @param {string | null | undefined} range
	 * @returns {[number, number | undefined] | undefined}
	 */
	function parseRange(range) {
		var m = /^\s*(\d+)\s*(?:(,)\s*(?:(\d+)\s*)?)?$/.exec(range || '');
		if (m) {
			var start = Number(m[1]);
			var comma = m[2];
			var end = m[3];

			if (!comma) {
				return [start, start];
			}
			if (!end) {
				return [start, undefined];
			}
			return [start, Number(end)];
		}
		return undefined;
	}

	Prism.hooks.add('before-highlightall', function (env) {
		env.selector += ', ' + SELECTOR;
	});

	Prism.hooks.add('before-sanity-check', function (env) {
		var pre = /** @type {HTMLPreElement} */ (env.element);
		if (pre.matches(SELECTOR)) {
			env.code = ''; // fast-path the whole thing and go to complete

			pre.setAttribute(STATUS_ATTR, STATUS_LOADING); // mark as loading

			// add code element with loading message
			var code = pre.appendChild(document.createElement('CODE'));
			code.textContent = LOADING_MESSAGE;

			var src = pre.getAttribute('data-src');

			var language = env.language;
			if (language === 'none') {
				// the language might be 'none' because there is no language set;
				// in this case, we want to use the extension as the language
				var extension = (/\.(\w+)$/.exec(src) || [, 'none'])[1];
				language = EXTENSIONS[extension] || extension;
			}

			// set language classes
			Prism.util.setLanguage(code, language);
			Prism.util.setLanguage(pre, language);

			// preload the language
			var autoloader = Prism.plugins.autoloader;
			if (autoloader) {
				autoloader.loadLanguages(language);
			}

			// load file
			loadFile(
				src,
				function (text) {
					// mark as loaded
					pre.setAttribute(STATUS_ATTR, STATUS_LOADED);

					// handle data-range
					var range = parseRange(pre.getAttribute('data-range'));
					if (range) {
						var lines = text.split(/\r\n?|\n/g);

						// the range is one-based and inclusive on both ends
						var start = range[0];
						var end = range[1] == null ? lines.length : range[1];

						if (start < 0) { start += lines.length; }
						start = Math.max(0, Math.min(start - 1, lines.length));
						if (end < 0) { end += lines.length; }
						end = Math.max(0, Math.min(end, lines.length));

						text = lines.slice(start, end).join('\n');

						// add data-start for line numbers
						if (!pre.hasAttribute('data-start')) {
							pre.setAttribute('data-start', String(start + 1));
						}
					}

					// highlight code
					code.textContent = text;
					Prism.highlightElement(code);
				},
				function (error) {
					// mark as failed
					pre.setAttribute(STATUS_ATTR, STATUS_FAILED);

					code.textContent = error;
				}
			);
		}
	});

	Prism.plugins.fileHighlight = {
		/**
		 * Executes the File Highlight plugin for all matching `pre` elements under the given container.
		 *
		 * Note: Elements which are already loaded or currently loading will not be touched by this method.
		 *
		 * @param {ParentNode} [container=document]
		 */
		highlight: function highlight(container) {
			var elements = (container || document).querySelectorAll(SELECTOR);

			for (var i = 0, element; (element = elements[i++]);) {
				Prism.highlightElement(element);
			}
		}
	};

	var logged = false;
	/** @deprecated Use `Prism.plugins.fileHighlight.highlight` instead. */
	Prism.fileHighlight = function () {
		if (!logged) {
			console.warn('Prism.fileHighlight is deprecated. Use `Prism.plugins.fileHighlight.highlight` instead.');
			logged = true;
		}
		Prism.plugins.fileHighlight.highlight.apply(this, arguments);
	};

}());

!function(){if("undefined"!=typeof Prism&&"undefined"!=typeof document){var e={javascript:"clike",actionscript:"javascript",apex:["clike","sql"],arduino:"cpp",aspnet:["markup","csharp"],birb:"clike",bison:"c",c:"clike",csharp:"clike",cpp:"c",cfscript:"clike",chaiscript:["clike","cpp"],cilkc:"c",cilkcpp:"cpp",coffeescript:"javascript",crystal:"ruby","css-extras":"css",d:"clike",dart:"clike",django:"markup-templating",ejs:["javascript","markup-templating"],etlua:["lua","markup-templating"],erb:["ruby","markup-templating"],fsharp:"clike","firestore-security-rules":"clike",flow:"javascript",ftl:"markup-templating",gml:"clike",glsl:"c",go:"clike",gradle:"clike",groovy:"clike",haml:"ruby",handlebars:"markup-templating",haxe:"clike",hlsl:"c",idris:"haskell",java:"clike",javadoc:["markup","java","javadoclike"],jolie:"clike",jsdoc:["javascript","javadoclike","typescript"],"js-extras":"javascript",json5:"json",jsonp:"json","js-templates":"javascript",kotlin:"clike",latte:["clike","markup-templating","php"],less:"css",lilypond:"scheme",liquid:"markup-templating",markdown:"markup","markup-templating":"markup",mongodb:"javascript",n4js:"javascript",objectivec:"c",opencl:"c",parser:"markup",php:"markup-templating",phpdoc:["php","javadoclike"],"php-extras":"php",plsql:"sql",processing:"clike",protobuf:"clike",pug:["markup","javascript"],purebasic:"clike",purescript:"haskell",qsharp:"clike",qml:"javascript",qore:"clike",racket:"scheme",cshtml:["markup","csharp"],jsx:["markup","javascript"],tsx:["jsx","typescript"],reason:"clike",ruby:"clike",sass:"css",scss:"css",scala:"java","shell-session":"bash",smarty:"markup-templating",solidity:"clike",soy:"markup-templating",sparql:"turtle",sqf:"clike",squirrel:"clike",stata:["mata","java","python"],"t4-cs":["t4-templating","csharp"],"t4-vb":["t4-templating","vbnet"],tap:"yaml",tt2:["clike","markup-templating"],textile:"markup",twig:"markup-templating",typescript:"javascript",v:"clike",vala:"clike",vbnet:"basic",velocity:"markup",wiki:"markup",xeora:"markup","xml-doc":"markup",xquery:"markup"},a={html:"markup",xml:"markup",svg:"markup",mathml:"markup",ssml:"markup",atom:"markup",rss:"markup",js:"javascript",g4:"antlr4",ino:"arduino","arm-asm":"armasm",art:"arturo",adoc:"asciidoc",avs:"avisynth",avdl:"avro-idl",gawk:"awk",sh:"bash",shell:"bash",shortcode:"bbcode",rbnf:"bnf",oscript:"bsl",cs:"csharp",dotnet:"csharp",cfc:"cfscript","cilk-c":"cilkc","cilk-cpp":"cilkcpp",cilk:"cilkcpp",coffee:"coffeescript",conc:"concurnas",jinja2:"django","dns-zone":"dns-zone-file",dockerfile:"docker",gv:"dot",eta:"ejs",xlsx:"excel-formula",xls:"excel-formula",gamemakerlanguage:"gml",po:"gettext",gni:"gn",ld:"linker-script","go-mod":"go-module",hbs:"handlebars",mustache:"handlebars",hs:"haskell",idr:"idris",gitignore:"ignore",hgignore:"ignore",npmignore:"ignore",webmanifest:"json",kt:"kotlin",kts:"kotlin",kum:"kumir",tex:"latex",context:"latex",ly:"lilypond",emacs:"lisp",elisp:"lisp","emacs-lisp":"lisp",md:"markdown",moon:"moonscript",n4jsd:"n4js",nani:"naniscript",objc:"objectivec",qasm:"openqasm",objectpascal:"pascal",px:"pcaxis",pcode:"peoplecode",plantuml:"plant-uml",pq:"powerquery",mscript:"powerquery",pbfasm:"purebasic",purs:"purescript",py:"python",qs:"qsharp",rkt:"racket",razor:"cshtml",rpy:"renpy",res:"rescript",robot:"robotframework",rb:"ruby","sh-session":"shell-session",shellsession:"shell-session",smlnj:"sml",sol:"solidity",sln:"solution-file",rq:"sparql",sclang:"supercollider",t4:"t4-cs",trickle:"tremor",troy:"tremor",trig:"turtle",ts:"typescript",tsconfig:"typoscript",uscript:"unrealscript",uc:"unrealscript",url:"uri",vb:"visual-basic",vba:"visual-basic",webidl:"web-idl",mathematica:"wolfram",nb:"wolfram",wl:"wolfram",xeoracube:"xeora",yml:"yaml"},r={},s="components/",i=Prism.util.currentScript();if(i){var t=/\bplugins\/autoloader\/prism-autoloader\.(?:min\.)?js(?:\?[^\r\n/]*)?$/i,c=/(^|\/)[\w-]+\.(?:min\.)?js(?:\?[^\r\n/]*)?$/i,l=i.getAttribute("data-autoloader-path");if(null!=l)s=l.trim().replace(/\/?$/,"/");else{var p=i.src;t.test(p)?s=p.replace(t,"components/"):c.test(p)&&(s=p.replace(c,"$1components/"))}}var n=Prism.plugins.autoloader={languages_path:s,use_minified:!0,loadLanguages:m};Prism.hooks.add("complete",(function(e){var a=e.element,r=e.language;if(a&&r&&"none"!==r){var s=function(e){var a=(e.getAttribute("data-dependencies")||"").trim();if(!a){var r=e.parentElement;r&&"pre"===r.tagName.toLowerCase()&&(a=(r.getAttribute("data-dependencies")||"").trim())}return a?a.split(/\s*,\s*/g):[]}(a);/^diff-./i.test(r)?(s.push("diff"),s.push(r.substr("diff-".length))):s.push(r),s.every(o)||m(s,(function(){Prism.highlightElement(a)}))}}))}function o(e){if(e.indexOf("!")>=0)return!1;if((e=a[e]||e)in Prism.languages)return!0;var s=r[e];return s&&!s.error&&!1===s.loading}function m(s,i,t){"string"==typeof s&&(s=[s]);var c=s.length,l=0,p=!1;function k(){p||++l===c&&i&&i(s)}0!==c?s.forEach((function(s){!function(s,i,t){var c=s.indexOf("!")>=0;function l(){var e=r[s];e||(e=r[s]={callbacks:[]}),e.callbacks.push({success:i,error:t}),!c&&o(s)?u(s,"success"):!c&&e.error?u(s,"error"):!c&&e.loading||(e.loading=!0,e.error=!1,function(e,a,r){var s=document.createElement("script");s.src=e,s.async=!0,s.onload=function(){document.body.removeChild(s),a&&a()},s.onerror=function(){document.body.removeChild(s),r&&r()},document.body.appendChild(s)}(function(e){return n.languages_path+"prism-"+e+(n.use_minified?".min":"")+".js"}(s),(function(){e.loading=!1,u(s,"success")}),(function(){e.loading=!1,e.error=!0,u(s,"error")})))}s=s.replace("!","");var p=e[s=a[s]||s];p&&p.length?m(p,l,t):l()}(s,k,(function(){p||(p=!0,t&&t(s))}))})):i&&setTimeout(i,0)}function u(e,a){if(r[e]){for(var s=r[e].callbacks,i=0,t=s.length;i<t;i++){var c=s[i][a];c&&setTimeout(c,0)}s.length=0}}}();
!function(){if("undefined"!=typeof Prism&&"undefined"!=typeof document){var e=[],t={},n=function(){};Prism.plugins.toolbar={};var a=Prism.plugins.toolbar.registerButton=function(n,a){var r;r="function"==typeof a?a:function(e){var t;return"function"==typeof a.onClick?((t=document.createElement("button")).type="button",t.addEventListener("click",(function(){a.onClick.call(this,e)}))):"string"==typeof a.url?(t=document.createElement("a")).href=a.url:t=document.createElement("span"),a.className&&t.classList.add(a.className),t.textContent=a.text,t},n in t?console.warn('There is a button with the key "'+n+'" registered already.'):e.push(t[n]=r)},r=Prism.plugins.toolbar.hook=function(a){var r=a.element.parentNode;if(r&&/pre/i.test(r.nodeName)&&!r.parentNode.classList.contains("code-toolbar")){var o=document.createElement("div");o.classList.add("code-toolbar"),r.parentNode.insertBefore(o,r),o.appendChild(r);var i=document.createElement("div");i.classList.add("toolbar");var l=e,d=function(e){for(;e;){var t=e.getAttribute("data-toolbar-order");if(null!=t)return(t=t.trim()).length?t.split(/\s*,\s*/g):[];e=e.parentElement}}(a.element);d&&(l=d.map((function(e){return t[e]||n}))),l.forEach((function(e){var t=e(a);if(t){var n=document.createElement("div");n.classList.add("toolbar-item"),n.appendChild(t),i.appendChild(n)}})),o.appendChild(i)}};a("label",(function(e){var t=e.element.parentNode;if(t&&/pre/i.test(t.nodeName)&&t.hasAttribute("data-label")){var n,a,r=t.getAttribute("data-label");try{a=document.querySelector("template#"+r)}catch(e){}return a?n=a.content:(t.hasAttribute("data-url")?(n=document.createElement("a")).href=t.getAttribute("data-url"):n=document.createElement("span"),n.textContent=r),n}})),Prism.hooks.add("complete",r)}}();
!function(){function t(t){var e=document.createElement("textarea");e.value=t.getText(),e.style.top="0",e.style.left="0",e.style.position="fixed",document.body.appendChild(e),e.focus(),e.select();try{var o=document.execCommand("copy");setTimeout((function(){o?t.success():t.error()}),1)}catch(e){setTimeout((function(){t.error(e)}),1)}document.body.removeChild(e)}"undefined"!=typeof Prism&&"undefined"!=typeof document&&(Prism.plugins.toolbar?Prism.plugins.toolbar.registerButton("copy-to-clipboard",(function(e){var o=e.element,n=function(t){var e={copy:"Copy","copy-error":"Press Ctrl+C to copy","copy-success":"Copied!","copy-timeout":5e3};for(var o in e){for(var n="data-prismjs-"+o,c=t;c&&!c.hasAttribute(n);)c=c.parentElement;c&&(e[o]=c.getAttribute(n))}return e}(o),c=document.createElement("button");c.className="copy-to-clipboard-button",c.setAttribute("type","button");var r=document.createElement("span");return c.appendChild(r),u("copy"),function(e,o){e.addEventListener("click",(function(){!function(e){navigator.clipboard?navigator.clipboard.writeText(e.getText()).then(e.success,(function(){t(e)})):t(e)}(o)}))}(c,{getText:function(){return o.textContent},success:function(){u("copy-success"),i()},error:function(){u("copy-error"),setTimeout((function(){!function(t){window.getSelection().selectAllChildren(t)}(o)}),1),i()}}),c;function i(){setTimeout((function(){u("copy")}),n["copy-timeout"])}function u(t){r.textContent=n[t],c.setAttribute("data-copy-state",t)}})):console.warn("Copy to Clipboard plugin loaded before Toolbar plugin."))}();
!function(){if("undefined"!=typeof Prism&&"undefined"!=typeof document)if(Prism.plugins.toolbar){var e={none:"Plain text",plain:"Plain text",plaintext:"Plain text",text:"Plain text",txt:"Plain text",html:"HTML",xml:"XML",svg:"SVG",mathml:"MathML",ssml:"SSML",rss:"RSS",css:"CSS",clike:"C-like",js:"JavaScript",abap:"ABAP",abnf:"ABNF",al:"AL",antlr4:"ANTLR4",g4:"ANTLR4",apacheconf:"Apache Configuration",apl:"APL",aql:"AQL",ino:"Arduino",arff:"ARFF",armasm:"ARM Assembly","arm-asm":"ARM Assembly",art:"Arturo",asciidoc:"AsciiDoc",adoc:"AsciiDoc",aspnet:"ASP.NET (C#)",asm6502:"6502 Assembly",asmatmel:"Atmel AVR Assembly",autohotkey:"AutoHotkey",autoit:"AutoIt",avisynth:"AviSynth",avs:"AviSynth","avro-idl":"Avro IDL",avdl:"Avro IDL",awk:"AWK",gawk:"GAWK",sh:"Shell",basic:"BASIC",bbcode:"BBcode",bbj:"BBj",bnf:"BNF",rbnf:"RBNF",bqn:"BQN",bsl:"BSL (1C:Enterprise)",oscript:"OneScript",csharp:"C#",cs:"C#",dotnet:"C#",cpp:"C++",cfscript:"CFScript",cfc:"CFScript",cil:"CIL",cilkc:"Cilk/C","cilk-c":"Cilk/C",cilkcpp:"Cilk/C++","cilk-cpp":"Cilk/C++",cilk:"Cilk/C++",cmake:"CMake",cobol:"COBOL",coffee:"CoffeeScript",conc:"Concurnas",csp:"Content-Security-Policy","css-extras":"CSS Extras",csv:"CSV",cue:"CUE",dataweave:"DataWeave",dax:"DAX",django:"Django/Jinja2",jinja2:"Django/Jinja2","dns-zone-file":"DNS zone file","dns-zone":"DNS zone file",dockerfile:"Docker",dot:"DOT (Graphviz)",gv:"DOT (Graphviz)",ebnf:"EBNF",editorconfig:"EditorConfig",ejs:"EJS",etlua:"Embedded Lua templating",erb:"ERB","excel-formula":"Excel Formula",xlsx:"Excel Formula",xls:"Excel Formula",fsharp:"F#","firestore-security-rules":"Firestore security rules",ftl:"FreeMarker Template Language",gml:"GameMaker Language",gamemakerlanguage:"GameMaker Language",gap:"GAP (CAS)",gcode:"G-code",gdscript:"GDScript",gedcom:"GEDCOM",gettext:"gettext",po:"gettext",glsl:"GLSL",gn:"GN",gni:"GN","linker-script":"GNU Linker Script",ld:"GNU Linker Script","go-module":"Go module","go-mod":"Go module",graphql:"GraphQL",hbs:"Handlebars",hs:"Haskell",hcl:"HCL",hlsl:"HLSL",http:"HTTP",hpkp:"HTTP Public-Key-Pins",hsts:"HTTP Strict-Transport-Security",ichigojam:"IchigoJam","icu-message-format":"ICU Message Format",idr:"Idris",ignore:".ignore",gitignore:".gitignore",hgignore:".hgignore",npmignore:".npmignore",inform7:"Inform 7",javadoc:"JavaDoc",javadoclike:"JavaDoc-like",javastacktrace:"Java stack trace",jq:"JQ",jsdoc:"JSDoc","js-extras":"JS Extras",json:"JSON",webmanifest:"Web App Manifest",json5:"JSON5",jsonp:"JSONP",jsstacktrace:"JS stack trace","js-templates":"JS Templates",keepalived:"Keepalived Configure",kts:"Kotlin Script",kt:"Kotlin",kumir:"KuMir (КуМир)",kum:"KuMir (КуМир)",latex:"LaTeX",tex:"TeX",context:"ConTeXt",lilypond:"LilyPond",ly:"LilyPond",emacs:"Lisp",elisp:"Lisp","emacs-lisp":"Lisp",llvm:"LLVM IR",log:"Log file",lolcode:"LOLCODE",magma:"Magma (CAS)",md:"Markdown","markup-templating":"Markup templating",matlab:"MATLAB",maxscript:"MAXScript",mel:"MEL",metafont:"METAFONT",mongodb:"MongoDB",moon:"MoonScript",n1ql:"N1QL",n4js:"N4JS",n4jsd:"N4JS","nand2tetris-hdl":"Nand To Tetris HDL",naniscript:"Naninovel Script",nani:"Naninovel Script",nasm:"NASM",neon:"NEON",nginx:"nginx",nsis:"NSIS",objectivec:"Objective-C",objc:"Objective-C",ocaml:"OCaml",opencl:"OpenCL",openqasm:"OpenQasm",qasm:"OpenQasm",parigp:"PARI/GP",objectpascal:"Object Pascal",psl:"PATROL Scripting Language",pcaxis:"PC-Axis",px:"PC-Axis",peoplecode:"PeopleCode",pcode:"PeopleCode",php:"PHP",phpdoc:"PHPDoc","php-extras":"PHP Extras","plant-uml":"PlantUML",plantuml:"PlantUML",plsql:"PL/SQL",powerquery:"PowerQuery",pq:"PowerQuery",mscript:"PowerQuery",powershell:"PowerShell",promql:"PromQL",properties:".properties",protobuf:"Protocol Buffers",purebasic:"PureBasic",pbfasm:"PureBasic",purs:"PureScript",py:"Python",qsharp:"Q#",qs:"Q#",q:"Q (kdb+ database)",qml:"QML",rkt:"Racket",cshtml:"Razor C#",razor:"Razor C#",jsx:"React JSX",tsx:"React TSX",renpy:"Ren'py",rpy:"Ren'py",res:"ReScript",rest:"reST (reStructuredText)",robotframework:"Robot Framework",robot:"Robot Framework",rb:"Ruby",sas:"SAS",sass:"Sass (Sass)",scss:"Sass (SCSS)","shell-session":"Shell session","sh-session":"Shell session",shellsession:"Shell session",sml:"SML",smlnj:"SML/NJ",solidity:"Solidity (Ethereum)",sol:"Solidity (Ethereum)","solution-file":"Solution file",sln:"Solution file",soy:"Soy (Closure Template)",sparql:"SPARQL",rq:"SPARQL","splunk-spl":"Splunk SPL",sqf:"SQF: Status Quo Function (Arma 3)",sql:"SQL",stata:"Stata Ado",iecst:"Structured Text (IEC 61131-3)",supercollider:"SuperCollider",sclang:"SuperCollider",systemd:"Systemd configuration file","t4-templating":"T4 templating","t4-cs":"T4 Text Templates (C#)",t4:"T4 Text Templates (C#)","t4-vb":"T4 Text Templates (VB)",tap:"TAP",tt2:"Template Toolkit 2",toml:"TOML",trickle:"trickle",troy:"troy",trig:"TriG",ts:"TypeScript",tsconfig:"TSConfig",uscript:"UnrealScript",uc:"UnrealScript",uorazor:"UO Razor Script",uri:"URI",url:"URL",vbnet:"VB.Net",vhdl:"VHDL",vim:"vim","visual-basic":"Visual Basic",vba:"VBA",vb:"Visual Basic",wasm:"WebAssembly","web-idl":"Web IDL",webidl:"Web IDL",wgsl:"WGSL",wiki:"Wiki markup",wolfram:"Wolfram language",nb:"Mathematica Notebook",wl:"Wolfram language",xeoracube:"XeoraCube","xml-doc":"XML doc (.net)",xojo:"Xojo (REALbasic)",xquery:"XQuery",yaml:"YAML",yml:"YAML",yang:"YANG"};Prism.plugins.toolbar.registerButton("show-language",(function(a){var t=a.element.parentNode;if(t&&/pre/i.test(t.nodeName)){var o,i=t.getAttribute("data-language")||e[a.language]||((o=a.language)?(o.substring(0,1).toUpperCase()+o.substring(1)).replace(/s(?=cript)/,"S"):o);if(i){var s=document.createElement("span");return s.textContent=i,s}}}))}else console.warn("Show Languages plugin loaded before Toolbar plugin.")}();
(function (Prism) {
    if (!Prism || !Prism.languages) {
        return;
    }

    Prism.languages.scriban = {
        "comment": [
            {
                pattern: /(^|[^\\])#.*/,
                lookbehind: true,
                greedy: true
            }
        ],
        "template-delimiter": {
            pattern: /\{\{[-~]?|[-~]?\}\}/,
            alias: "punctuation"
        },
        "string": [
            {
                pattern: /@?"(?:\\.|[^"\\\r\n])*"/,
                greedy: true
            },
            {
                pattern: /'(?:\\.|[^'\\\r\n])*'/,
                greedy: true
            }
        ],
        "interpolation": {
            pattern: /(^|[^\\])\$\{(?:[^{}]|{[^{}]*})*\}/,
            lookbehind: true,
            inside: {
                "interpolation-punctuation": {
                    pattern: /^\$\{|\}$/,
                    alias: "punctuation"
                }
            }
        },
        "keyword": /\b(?:as|break|case|capture|continue|do|else|end|for|func|if|import|in|readonly|ret|return|tablerow|when|while|with)\b/,
        "boolean": /\b(?:false|null|true)\b/,
        "builtin": /\b(?:empty)\b/,
        "number": /\b(?:0x[\da-f]+|(?:\d+\.?\d*|\.\d+)(?:e[+-]?\d+)?)\b/i,
        "function": /\b[a-z_]\w*(?=\s*(?:\())/i,
        "variable": /\$[a-z_]\w*/i,
        "operator": /\?\?|\.\.|=>|==|!=|<=|>=|&&|\|\||[+\-*/%!?=<>|&^~]/,
        "punctuation": /[()[\]{}.,:;]/
    };

    Prism.languages.sbn = Prism.languages.scriban;
    Prism.languages["scriban-html"] = Prism.languages.scriban;
    Prism.languages["scriban-md"] = Prism.languages.scriban;
}(Prism));

const lunetCssPrefix = document.documentElement.dataset.lunetCssPrefix || 'lunet';

// Ensure copy-to-clipboard buttons get Bootstrap tooltips
document.addEventListener('DOMContentLoaded', () => {
  const rootElement = document.documentElement;
  const themeStorageKey = rootElement.dataset.lunetThemeStorageKey || 'lunet-theme';
  const defaultThemeMode = rootElement.dataset.lunetThemeMode || 'system';
  const themeMediaQuery = window.matchMedia('(prefers-color-scheme: dark)');
  const themeModeButtons = document.querySelectorAll('[data-lunet-theme-choice]');
  const themeToggleIcon = document.querySelector('[data-lunet-theme-toggle-icon]');
  const themeToggleLabel = document.querySelector('[data-lunet-theme-toggle-label]');
  const themeModeLabels = {
    system: themeToggleLabel?.dataset.themeSystemLabel || 'System',
    light: themeToggleLabel?.dataset.themeLightLabel || 'Light',
    dark: themeToggleLabel?.dataset.themeDarkLabel || 'Dark',
  };

  function sanitizeThemeMode(mode) {
    return mode === 'light' || mode === 'dark' || mode === 'system' ? mode : defaultThemeMode;
  }

  function resolveThemeMode(mode) {
    if (mode === 'system') {
      return themeMediaQuery.matches ? 'dark' : 'light';
    }
    return mode;
  }

  function getStoredThemeMode() {
    try {
      return sanitizeThemeMode(localStorage.getItem(themeStorageKey));
    } catch {
      return defaultThemeMode;
    }
  }

  function saveThemeMode(mode) {
    try {
      localStorage.setItem(themeStorageKey, mode);
    } catch {
    }
  }

  function updateThemeToggle(mode) {
    if (themeToggleLabel) {
      themeToggleLabel.textContent = `${themeModeLabels[mode] ?? mode}`;
    }

    if (themeToggleIcon) {
      themeToggleIcon.className = 'bi';
      if (mode === 'light') {
        themeToggleIcon.classList.add('bi-sun-fill');
      } else if (mode === 'dark') {
        themeToggleIcon.classList.add('bi-moon-stars-fill');
      } else {
        themeToggleIcon.classList.add('bi-circle-half');
      }
    }

    themeModeButtons.forEach(button => {
      const isActive = button.dataset.lunetThemeChoice === mode;
      button.classList.toggle('active', isActive);
      button.setAttribute('aria-pressed', isActive ? 'true' : 'false');
    });
  }

  function applyThemeMode(mode, persist) {
    const sanitizedMode = sanitizeThemeMode(mode);
    const resolvedTheme = resolveThemeMode(sanitizedMode);
    rootElement.setAttribute('data-lunet-theme-mode', sanitizedMode);
    rootElement.setAttribute('data-bs-theme', resolvedTheme);
    if (persist) {
      saveThemeMode(sanitizedMode);
    }
    updateThemeToggle(sanitizedMode);
  }

  applyThemeMode(getStoredThemeMode(), false);

  themeModeButtons.forEach(button => {
    button.addEventListener('click', () => {
      applyThemeMode(button.dataset.lunetThemeChoice, true);
    });
  });

  const handleSystemThemeChange = () => {
    if ((rootElement.getAttribute('data-lunet-theme-mode') || defaultThemeMode) === 'system') {
      applyThemeMode('system', false);
    }
  };

  if (typeof themeMediaQuery.addEventListener === 'function') {
    themeMediaQuery.addEventListener('change', handleSystemThemeChange);
  } else if (typeof themeMediaQuery.addListener === 'function') {
    themeMediaQuery.addListener(handleSystemThemeChange);
  }

  const copyTooltipScope = document.querySelector('section[data-copy-tooltip]');
  const copyTooltipText = copyTooltipScope?.dataset.copyTooltip || 'Copy to Clipboard.';
  const copyTooltipSuccessText = copyTooltipScope?.dataset.copyTooltipSuccess || 'Copied!';
  const copyTooltipErrorText = copyTooltipScope?.dataset.copyTooltipError || 'Failed to copy!';

  const copyBtns = document.querySelectorAll('button.copy-to-clipboard-button');

  copyBtns.forEach(btn => {
    // Ensure tooltip attributes exist
    if (!btn.hasAttribute('data-bs-toggle')) btn.setAttribute('data-bs-toggle', 'tooltip');
    if (!btn.hasAttribute('data-bs-placement')) btn.setAttribute('data-bs-placement', 'left');
    if (!btn.hasAttribute('data-bs-title')) btn.setAttribute('data-bs-title', copyTooltipText);

    const tt = bootstrap.Tooltip.getOrCreateInstance(btn);
    const original = btn.getAttribute('data-bs-title') || copyTooltipText;

    // Hide tooltip on click (if visible from hover)
    //btn.addEventListener('click', () => tt.hide());

    // React to Prism's data-copy-state changes
    const obs = new MutationObserver(muts => {
      for (const m of muts) {
        if (m.type === 'attributes' && m.attributeName === 'data-copy-state') {
          const state = btn.getAttribute('data-copy-state');

          if (state === 'copy-success') {
            if (typeof tt.setContent === 'function') tt.setContent({ '.tooltip-inner': copyTooltipSuccessText });
            else btn.setAttribute('data-bs-title', copyTooltipSuccessText);
            tt.show();
            // Drop focus so the button doesn’t stay highlighted
            btn.blur();
            setTimeout(() => {
              if (typeof tt.setContent === 'function') tt.setContent({ '.tooltip-inner': original });
              else btn.setAttribute('data-bs-title', original);
              tt.hide();
            }, 1200);
          } else if (state === 'copy-error') {
            if (typeof tt.setContent === 'function') tt.setContent({ '.tooltip-inner': copyTooltipErrorText });
            else btn.setAttribute('data-bs-title', copyTooltipErrorText);
            tt.show();
            // Drop focus so the button doesn’t stay highlighted
            btn.blur();
            setTimeout(() => {
              if (typeof tt.setContent === 'function') tt.setContent({ '.tooltip-inner': original });
              else btn.setAttribute('data-bs-title', original);
              tt.hide();
            }, 1500);
          }
        }
      }
    });
    obs.observe(btn, { attributes: true, attributeFilter: ['data-copy-state'] });
  });

  function wrapTerminalElement(element) {
      if (!element || element.closest('.terminal-window')) return;

      // Only wrap elements that are rendered as a "terminal screenshot" in docs.
      const tagName = element.tagName?.toUpperCase();
      if (tagName !== 'IMG' && tagName !== 'SVG' && tagName !== 'PRE' && tagName !== 'VIDEO') return;

      const titleText =
          element.getAttribute('data-terminal-title') ||
          element.getAttribute('title') ||
          element.getAttribute('alt') ||
          '';

      const wrapper = document.createElement('figure');
      wrapper.className = 'terminal-window';
      if (element.classList.contains('terminal-hero')) {
          wrapper.classList.add('terminal-window--hero');
      }

      const bar = document.createElement('div');
      bar.className = 'terminal-window__bar';

      const controls = document.createElement('div');
      controls.className = 'terminal-window__controls';

      const dotRed = document.createElement('span');
      dotRed.className = 'terminal-window__dot terminal-window__dot--red';
      const dotYellow = document.createElement('span');
      dotYellow.className = 'terminal-window__dot terminal-window__dot--yellow';
      const dotGreen = document.createElement('span');
      dotGreen.className = 'terminal-window__dot terminal-window__dot--green';
      controls.append(dotRed, dotYellow, dotGreen);

      const title = document.createElement('div');
      title.className = 'terminal-window__title';
      title.textContent = titleText;
      title.title = titleText;

      bar.append(controls, title);

      const content = document.createElement('div');
      content.className = 'terminal-window__content';

      const parent = element.parentNode;
      if (!parent) return;

      parent.insertBefore(wrapper, element);
      wrapper.append(bar, content);
      content.appendChild(element);
  }

  document.querySelectorAll('.terminal').forEach(wrapTerminalElement);

  function scrollSidebarToActiveItem(sidebar) {
      if (!sidebar || sidebar.scrollHeight <= sidebar.clientHeight) return;

      const activeItem = sidebar.querySelector('li.menu-item.active');
      if (!activeItem) return;

      const target = activeItem.querySelector('a.menu-link') || activeItem;
      requestAnimationFrame(() => {
          const sidebarRect = sidebar.getBoundingClientRect();
          const targetRect = target.getBoundingClientRect();
          const targetTopInSidebar = (targetRect.top - sidebarRect.top) + sidebar.scrollTop;
          const desiredTop = targetTopInSidebar - (sidebar.clientHeight / 2) + (targetRect.height / 2);
          const maxTop = sidebar.scrollHeight - sidebar.clientHeight;
          sidebar.scrollTop = Math.max(0, Math.min(desiredTop, maxTop));
      });
  }

  function expandMenuAncestors(sidebar) {
      if (!sidebar) return;

      sidebar.querySelectorAll('li.menu-item.active').forEach((activeItem) => {
          let collapseList = activeItem.closest('ol.collapse');
          while (collapseList) {
              collapseList.classList.add('show');
              if (collapseList.id) {
                  const toggles = sidebar.querySelectorAll(`a.menu-link-show[href='#${collapseList.id}']`);
                  toggles.forEach((toggle) => {
                      toggle.classList.remove('collapsed');
                      toggle.setAttribute('aria-expanded', 'true');
                  });
              }

              const parentItem = collapseList.closest('li.menu-item');
              collapseList = parentItem ? parentItem.closest('ol.collapse') : null;
          }
      });

      sidebar.querySelectorAll("a.menu-link-show[aria-expanded='true']").forEach((toggle) => {
          const href = toggle.getAttribute('href');
          if (!href || !href.startsWith('#')) return;
          const collapseList = sidebar.querySelector(href);
          if (collapseList && collapseList.classList.contains('collapse')) {
              collapseList.classList.add('show');
          }
          toggle.classList.remove('collapsed');
      });
  }

  document.querySelectorAll('.menu-sidebar').forEach((sidebar) => {
      expandMenuAncestors(sidebar);
      scrollSidebarToActiveItem(sidebar);
  });
});

// Initialize bootstrap tooltips
const tooltipTriggerList = document.querySelectorAll('[data-bs-toggle="tooltip"]')
const tooltipList = [...tooltipTriggerList].map(tooltipTriggerEl => new bootstrap.Tooltip(tooltipTriggerEl))

var jstoc = document.getElementsByClassName("js-toc");
if (jstoc.length > 0)
{
    tocbot.init({
        // Where to render the table of contents.
        tocSelector: '.js-toc',
        // Where to grab the headings to build the table of contents.
        contentSelector: '.js-toc-content',
        // Which headings to grab inside of the contentSelector element.
        headingSelector: 'h2, h3, h4',
        collapseDepth: 3,
        // Ensure correct positioning
        hasInnerContainers: true,
    });
}

var searchInput = document.getElementById("search-input");
var searchMenu = document.getElementById("search-results");
if (searchInput && searchMenu) {
    const emptySearchMessage = searchInput.dataset.searchEmptyMessage || 'Enter words to search...';
    const noSearchResultsMessage = searchInput.dataset.searchNoResultsMessage || 'No results found.';

    // Enable deep-link anchors only when search is present
    anchors.add(`.${lunetCssPrefix}-docs h2`);

    // Container
    const container = searchInput.closest(`.${lunetCssPrefix}-search`) || searchInput.parentElement;

    // Bootstrap Dropdown controller (guard if Bootstrap JS not present)
    var dropdown = (window.bootstrap && bootstrap.Dropdown)
        ? new bootstrap.Dropdown(searchInput, { autoClose: 'outside', display: 'static' })
        : null;

    let activeIndex = -1;
    let items = [];
    let lastQueryId = 0;

    function clearMenu() {
        searchMenu.innerHTML = '';
        items = [];
        activeIndex = -1;
    }

    function showMenu() {
        if (dropdown) {
            if (!searchMenu.classList.contains('show')) dropdown.show();
        } else {
            searchMenu.classList.add('show');
        }
        searchInput.setAttribute('aria-expanded', 'true');
    }

    function hideMenu() {
        if (dropdown) {
            if (searchMenu.classList.contains('show')) dropdown.hide();
        } else {
            searchMenu.classList.remove('show');
        }
        searchInput.setAttribute('aria-expanded', 'false');
    }

    function setActive(index) {
        if (index < -1 || index >= items.length) return;
        if (activeIndex >= 0 && items[activeIndex]) items[activeIndex].classList.remove('active');
        activeIndex = index;
        if (activeIndex >= 0 && items[activeIndex]) {
            items[activeIndex].classList.add('active');
            items[activeIndex].scrollIntoView({ block: 'nearest' });
        }
    }

    function renderMessage(message) {
        clearMenu();
        const li = document.createElement('li');
        const span = document.createElement('span');
        span.className = 'dropdown-item-text text-body-secondary';
        span.textContent = message;
        li.appendChild(span);
        searchMenu.appendChild(li);
        showMenu();
    }

    function renderRows(rows) {
        clearMenu();
        if (!rows || rows.length === 0) {
            renderMessage(noSearchResultsMessage);
            return;
        }
        const frag = document.createDocumentFragment();
        rows.forEach((row, i) => {
            const li = document.createElement('li');
            li.innerHTML = `<a class="dropdown-item" href="${row.url}">
                              <div class="fw-semibold">${row.title}</div>
                              <div class="small text-body-secondary">${row.snippet}</div>
                            </a>`;
            li.addEventListener('mouseover', () => setActive(i));
            frag.appendChild(li);
        });
        searchMenu.appendChild(frag);
        items = Array.from(searchMenu.querySelectorAll('.dropdown-item'));
        setActive(-1);
        showMenu();
    }

    function performSearch(term) {
        const queryId = ++lastQueryId;
        const q = (term || '').trim();
        if (!q) {
            clearMenu();
            renderMessage(emptySearchMessage);
            return;
        }
        DefaultLunetSearch.query(q).then(rows => {
            if (queryId !== lastQueryId) return; // stale
            renderRows(rows);
        }).catch(() => {
            if (queryId !== lastQueryId) return;
            renderMessage(noSearchResultsMessage);
        });
    }

    // Events
    searchInput.addEventListener('input', () => {
        performSearch(searchInput.value);
    });

    searchInput.addEventListener('keydown', (e) => {
        if (e.key === 'ArrowDown') {
            e.preventDefault();
            if (!searchMenu.classList.contains('show')) showMenu();
            setActive(Math.min(activeIndex + 1, items.length - 1));
        } else if (e.key === 'ArrowUp') {
            e.preventDefault();
            setActive(Math.max(activeIndex - 1, -1));
        } else if (e.key === 'Enter') {
            if (activeIndex >= 0 && items[activeIndex]) {
                e.preventDefault();
                items[activeIndex].click();
            }
        } else if (e.key === 'Escape') {
            hideMenu();
        }
    });

    // Alt+S to focus
    document.addEventListener('keydown', (e) => {
        if (e.altKey && (e.key === 's' || e.key === 'S')) {
            searchInput.focus();
            if (searchInput.value && !searchMenu.classList.contains('show')) showMenu();
            e.preventDefault();
        }
    });

    // Show helper on focus
    searchInput.addEventListener('focus', () => {
        if (!searchInput.value) renderMessage(emptySearchMessage);
        else showMenu();
    });

    // Click-away handling if Bootstrap isn't managing it
    if (!dropdown) {
        document.addEventListener('click', (e) => {
            if (container && !container.contains(e.target)) hideMenu();
        });
    }
}


