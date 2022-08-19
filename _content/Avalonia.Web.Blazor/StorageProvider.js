// TODO move to another file and use import
class IndexedDbWrapper {
    constructor(databaseName, objectStores) {
        this.databaseName = databaseName;
        this.objectStores = objectStores;
    }
    connect() {
        const conn = window.indexedDB.open(this.databaseName, 1);
        conn.onupgradeneeded = event => {
            const db = event.target.result;
            this.objectStores.forEach(store => {
                db.createObjectStore(store);
            });
        };
        return new Promise((resolve, reject) => {
            conn.onsuccess = event => {
                resolve(new InnerDbConnection(event.target.result));
            };
            conn.onerror = event => {
                reject(event.target.error);
            };
        });
    }
}
class InnerDbConnection {
    constructor(database) {
        this.database = database;
    }
    openStore(store, mode) {
        const tx = this.database.transaction(store, mode);
        return tx.objectStore(store);
    }
    put(store, obj, key) {
        const os = this.openStore(store, "readwrite");
        return new Promise((resolve, reject) => {
            const response = os.put(obj, key);
            response.onsuccess = () => {
                resolve(response.result);
            };
            response.onerror = () => {
                reject(response.error);
            };
        });
    }
    get(store, key) {
        const os = this.openStore(store, "readonly");
        return new Promise((resolve, reject) => {
            const response = os.get(key);
            response.onsuccess = () => {
                resolve(response.result);
            };
            response.onerror = () => {
                reject(response.error);
            };
        });
    }
    delete(store, key) {
        const os = this.openStore(store, "readwrite");
        return new Promise((resolve, reject) => {
            const response = os.delete(key);
            response.onsuccess = () => {
                resolve();
            };
            response.onerror = () => {
                reject(response.error);
            };
        });
    }
    close() {
        this.database.close();
    }
}
const fileBookmarksStore = "fileBookmarks";
const avaloniaDb = new IndexedDbWrapper("AvaloniaDb", [
    fileBookmarksStore
]);
class StorageItem {
    constructor(handle, bookmarkId) {
        this.handle = handle;
        this.bookmarkId = bookmarkId;
    }
    getName() {
        return this.handle.name;
    }
    getKind() {
        return this.handle.kind;
    }
    async openRead() {
        await this.verityPermissions('read');
        return await this.handle.getFile();
    }
    async openWrite() {
        await this.verityPermissions('readwrite');
        return await this.handle.createWritable({ keepExistingData: true });
    }
    async getProperties() {
        const file = this.handle.getFile && await this.handle.getFile();
        return file && {
            Size: file.size,
            LastModified: file.lastModified,
            Type: file.type
        };
    }
    async getItems() {
        if (this.handle.kind !== "directory") {
            return new StorageItems([]);
        }
        const items = [];
        for await (const [key, value] of this.handle.entries()) {
            items.push(new StorageItem(value));
        }
        return new StorageItems(items);
    }
    async verityPermissions(mode) {
        if (await this.handle.queryPermission({ mode }) === 'granted') {
            return;
        }
        if (await this.handle.requestPermission({ mode }) === "denied") {
            throw new Error("Read permissions denied");
        }
    }
    async saveBookmark() {
        // If file was previously bookmarked, just return old one.
        if (this.bookmarkId) {
            return this.bookmarkId;
        }
        const connection = await avaloniaDb.connect();
        try {
            const key = await connection.put(fileBookmarksStore, this.handle, this.generateBookmarkId());
            return key;
        }
        finally {
            connection.close();
        }
    }
    async deleteBookmark() {
        if (!this.bookmarkId) {
            return;
        }
        const connection = await avaloniaDb.connect();
        try {
            const key = await connection.delete(fileBookmarksStore, this.bookmarkId);
        }
        finally {
            connection.close();
        }
    }
    generateBookmarkId() {
        return Date.now().toString(36) + Math.random().toString(36).substring(2);
    }
}
class StorageItems {
    constructor(items) {
        this.items = items;
    }
    count() {
        return this.items.length;
    }
    at(index) {
        return this.items[index];
    }
}
export class StorageProvider {
    static canOpen() {
        return typeof window.showOpenFilePicker !== 'undefined';
    }
    static canSave() {
        return typeof window.showSaveFilePicker !== 'undefined';
    }
    static canPickFolder() {
        return typeof window.showDirectoryPicker !== 'undefined';
    }
    static async selectFolderDialog(startIn) {
        // 'Picker' API doesn't accept "null" as a parameter, so it should be set to undefined.
        const options = {
            startIn: (startIn?.handle || undefined)
        };
        const handle = await window.showDirectoryPicker(options);
        return new StorageItem(handle);
    }
    static async openFileDialog(startIn, multiple, types, excludeAcceptAllOption) {
        const options = {
            startIn: (startIn?.handle || undefined),
            multiple,
            excludeAcceptAllOption,
            types: (types || undefined)
        };
        const handles = await window.showOpenFilePicker(options);
        return new StorageItems(handles.map(handle => new StorageItem(handle)));
    }
    static async saveFileDialog(startIn, suggestedName, types, excludeAcceptAllOption) {
        const options = {
            startIn: (startIn?.handle || undefined),
            suggestedName: (suggestedName || undefined),
            excludeAcceptAllOption,
            types: (types || undefined)
        };
        const handle = await window.showSaveFilePicker(options);
        return new StorageItem(handle);
    }
    static async openBookmark(key) {
        const connection = await avaloniaDb.connect();
        try {
            const handle = await connection.get(fileBookmarksStore, key);
            return handle && new StorageItem(handle, key);
        }
        finally {
            connection.close();
        }
    }
}
//# sourceMappingURL=StorageProvider.js.map