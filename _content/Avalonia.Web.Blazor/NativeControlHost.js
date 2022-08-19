export class NativeControlHost {
    static CreateDefaultChild(parent) {
        return document.createElement("div");
    }
    // Used to convert ElementReference to JSObjectReference.
    // Is there a better way?
    static GetReference(element) {
        return element;
    }
    static CreateAttachment() {
        return new NativeControlHostTopLevelAttachment();
    }
}
class NativeControlHostTopLevelAttachment {
    InitializeWithChildHandle(child) {
        this._child = child;
        this._child.style.position = "absolute";
    }
    AttachTo(host) {
        if (this._host) {
            this._host.removeChild(this._child);
        }
        this._host = host;
        if (this._host) {
            this._host.appendChild(this._child);
        }
    }
    ShowInBounds(x, y, width, height) {
        this._child.style.top = y + "px";
        this._child.style.left = x + "px";
        this._child.style.width = width + "px";
        this._child.style.height = height + "px";
        this._child.style.display = "block";
    }
    HideWithSize(width, height) {
        this._child.style.width = width + "px";
        this._child.style.height = height + "px";
        this._child.style.display = "none";
    }
    ReleaseChild() {
        this._child = null;
    }
}
//# sourceMappingURL=NativeControlHost.js.map