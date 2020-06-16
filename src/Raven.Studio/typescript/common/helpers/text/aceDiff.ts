/// <reference path="../../../../typings/tsd.d.ts" />

import diff = require("diff");
//import DiffMatchPatch from 'diff-match-patch';
//const DiffMatchPatch = require('diff-match-patch');

type gapItem = {
    firstLine: number;
    emptyLinesCount: number;
}

type foldItem = {
    firstLine: number;
    lines: number;
}

class aceDiffEditor {
    static foldClassName = "diff_fold";
    
    private highlights: number[];
    private hasAnyChange: boolean;
    private folds = [] as foldItem[];
    private readonly editor: AceAjax.Editor;
    private readonly mode: "left" | "right";
    private gutterClass: string;
    private markerClass: string;
    private previousAceMode: string;
    private onModeChange: () => void;
    
    private onScroll: (scroll: any) => void;
    private onFold: (foldEvent: any) => void;
    private markers = [] as number[];
    private widgets = [] as any[];
    
    private gutters = [] as Array<{ row: number, className: string, idx?: number }>;
    
    constructor(editor: AceAjax.Editor, mode: "left" | "right", gutterClass: string, markerClass: string) {
        this.editor = editor;
        this.mode = mode;
        this.gutterClass = gutterClass;
        this.markerClass = markerClass;
        
        this.initEditor();
    }

    getAllLines() {
        return this.getSession().getDocument().getAllLines();
    }

    private initEditor() {
        const session = this.getSession() as any;
        if (!session.widgetManager) {
            const LineWidgets = ace.require("ace/line_widgets").LineWidgets;
            session.widgetManager = new LineWidgets(session);
            session.widgetManager.attach(this.editor);
        }
        
        this.previousAceMode = this.getSession().getMode().$id;

        this.onModeChange = () => this.modeChanged();
        this.getSession().on("changeMode", this.onModeChange);
    }

    private modeChanged() {
        const mode = this.getSession().getMode();
        
        if (mode.$id === "ace/mode/raven_document_diff" && this.hasAnyChange) {
            this.getSession().foldAll();
        }
    }
    
    refresh(gutterClass: string, markerClass: string) {
        this.destroy();

        this.gutterClass = gutterClass;
        this.markerClass = markerClass;
        
        this.initEditor();
    }
    
    getSession() {
        return this.editor.getSession();
    }
    
    getHighlightsCount() {
        return this.highlights.length;
    }
    
    update(patch: diff.IUniDiff, gaps: gapItem[]) {
        this.widgets = this.applyLineGaps(this.editor, gaps);
        this.highlights = this.findLinesToHighlight(patch.hunks, this.mode);
        this.hasAnyChange = patch.hunks.length > 0;
        this.folds = this.findFolds(this.highlights, gaps);
        this.decorateGutter(this.editor, this.gutterClass, this.highlights);
        this.createLineMarkers();
        this.addFolds();

        this.getSession().setMode("ace/mode/raven_document_diff");
    }
    
    private addFolds() {
        const session = this.getSession();
        this.folds.forEach((fold, idx) => {
            const linesClass = "diff_l_" + fold.lines;
            
            this.addDisposableGutterDecoration(session, fold.firstLine, aceDiffEditor.foldClassName, idx);
            this.addDisposableGutterDecoration(session, fold.firstLine, linesClass);
        })
    }
    
    private findFolds(highlightedLines: Array<number>, gaps: gapItem[]): Array<foldItem> {
        const totalLines = this.editor.getSession().getDocument().getLength();
        const bits = new Array(totalLines).fill(0);
        
        const context = 3;
        
        gaps.forEach(gap => {
            const l = gap.firstLine - 1;
            for (let i = Math.max(l - context, 0); i < Math.min(l + context, totalLines); i++) {
                bits[i] = 1;
            }
        });
        
        highlightedLines.forEach(l => {
            l = l - 1;
            for (let i = Math.max(l - context, 0); i < Math.min(l + context + 1, totalLines); i++) {
                bits[i] = 1;
            }
        });
        
        let inFold = false;
        let foldStart = -1;
        
        const result = [] as Array<foldItem>;
        for (let i = 0; i < totalLines; i++) {
            if (bits[i] === 0 && !inFold) {
                foldStart = i;
                inFold = true;
            }
            
            if (bits[i] === 1 && inFold) {
                result.push({
                    firstLine: foldStart,
                    lines: i - foldStart
                });
                inFold = false;
            }
        }
        
        if (inFold) {
            result.push({
                firstLine: foldStart,
                lines: totalLines - foldStart
            })
        }
        
        return result;
    }

    private createLineMarkers() {
        const marker = this.createLineHighlightMarker(this.markerClass, () => this.highlights);
        this.getSession().addDynamicMarker(marker, false);
        this.markers.push(marker.id);
    }

    private createLineHighlightMarker(className: string, linesProvider: () => Array<number>) {
        const AceRange = ace.require("ace/range").Range;

        return {
            id: undefined as number,
            update: (html: string[], marker: any, session: AceAjax.IEditSession, config: any) => {
                const lines = linesProvider();

                lines.forEach(line => {
                    const range = new AceRange(line - 1, 0, line - 1, Infinity);
                    if (range.clipRows(config.firstRow, config.lastRow).isEmpty()) {
                        return;
                    }

                    const screenRange = range.toScreenRange(session);
                    marker.drawScreenLineMarker(html, screenRange, className, config);
                });
            }
        }
    }

    private decorateGutter(editor: AceAjax.Editor, className: string, rows: Array<number>) {
        for (let i = 0; i < rows.length; i++) {
            this.addDisposableGutterDecoration(editor.getSession(), rows[i] - 1, className);
        }
    }
    
    private addDisposableGutterDecoration(session: AceAjax.IEditSession, row: number, className: string, idx?: number) {
        session.addGutterDecoration(row, className);
        
        this.gutters.push({
            className: className,
            row: row,
            idx: idx
        });
    }

    private findLinesToHighlight(hunks: diff.IHunk[], mode: "left" | "right") {
        const ignoreLinesStartsWith = mode === "left" ? "+" : "-";
        const takeLinesStartsWith = mode === "left" ? "-" : "+";

        const result = [] as Array<number>;
        hunks.forEach(hunk => {
            const startLine = mode === "left" ? hunk.oldStart : hunk.newStart;

            const filteredLines = hunk.lines.filter(x => !x.startsWith(ignoreLinesStartsWith));
            for (let i = 0; i < filteredLines.length; i++) {
                const line = filteredLines[i];
                if (line.startsWith(takeLinesStartsWith)) {
                    result.push(startLine + i);
                }
            }
        });
        return result;
    }

    private applyLineGaps(editor: AceAjax.Editor, gaps: Array<gapItem>) {
        const dom = ace.require("ace/lib/dom");
        const widgetManager = editor.getSession().widgetManager;
        const lineHeight = editor.renderer.layerConfig.lineHeight;

        return gaps.map(gap => {
            const element = dom.createElement("div") as HTMLElement;
            element.className = "difference_gap";
            element.style.height = gap.emptyLinesCount * lineHeight + "px";

            const widget = {
                row: gap.firstLine - 2,
                fixedWidth: true,
                coverGutter: false,
                el: element,
                type: "diffGap"
            };

            widgetManager.addLineWidget(widget);

            return widget;
        });
    }

    private cleanupGutter(editor: AceAjax.Editor) {
        const session = editor.getSession();
        for (let i = 0; i < this.gutters.length; i++) {
            const toClean = this.gutters[i];
            session.removeGutterDecoration(toClean.row, toClean.className);
        }
        
        this.gutters = [];
    }
    
    synchronizeScroll(secondEditor: aceDiffEditor) {
        this.onScroll = scroll => {
            const otherSession = secondEditor.getSession();
            if (scroll !== otherSession.getScrollTop()) {
                otherSession.setScrollTop(scroll || 0);
            }
        };
        
        this.getSession().on("changeScrollTop", this.onScroll);
    }
    
    synchronizeFolds(secondEditor: aceDiffEditor) {
        this.onFold = e => {
            const action = e.action;
            const startLine = e.data.start.row;
            
            const fold = this.gutters.find(x => x.row === startLine && x.className === aceDiffEditor.foldClassName);
            
            if (fold) {
                switch (action) {
                    case "add":
                        secondEditor.addFold(fold.idx);
                        break;
                    case "remove":
                        secondEditor.removeFold(fold.idx);
                        break;
                }
            }
        };
        
        this.getSession().on("changeFold", this.onFold);
    }
    
    addFold(idx: number) {
        const gutter = this.gutters.find(x => x.idx === idx && x.className === aceDiffEditor.foldClassName);
        
        const existingFold = this.getSession().getFoldAt(gutter.row, 0);
        if (existingFold) {
            return;
        }
        
        const range = this.getSession().getFoldWidgetRange(gutter.row);
        
        this.getSession().addFold("...", range);
    }
    
    removeFold(idx: number) {
        const gutter = this.gutters.find(x => x.idx === idx && x.className === aceDiffEditor.foldClassName);
        if (gutter) {
            const fold = this.getSession().getFoldAt(gutter.row, 0);
            if (fold) {
                this.getSession().removeFold(fold);
            }
        }
    }
    
    destroy() {
        if (this.onScroll) {
            this.getSession().off("changeScrollTop", this.onScroll);
            this.onScroll = null;
        }
        
        if (this.onFold) {
            this.getSession().off("changeFold", this.onFold);
            this.onFold = null;
        }
        
        this.cleanupGutter(this.editor);
        
        this.highlights = [];
        
        this.markers.forEach(marker => this.getSession().removeMarker(marker));
        this.markers = [];
        
        this.widgets.forEach(widget => this.getSession().widgetManager.removeLineWidget(widget));

        this.getSession().off("changeMode", this.onModeChange);
        
        this.getSession().setMode(this.previousAceMode);
    }
}

//
// constants from ace-diff
//
const DIFF_EQUAL = 0;
const DIFF_DELETE= -1;
const DIFF_INSERT = 1;
const EDITOR_RIGHT = 'right';
const EDITOR_LEFT = 'left';
const RTL = 'rtl';
const LTR = 'ltr';
const SVG_NS = 'http://www.w3.org/2000/svg';
const DIFF_GRANULARITY_SPECIFIC = 'specific';
const DIFF_GRANULARITY_BROAD = 'broad';

class aceDiff {
    
    private readonly leftEditor: aceDiffEditor;
    private readonly rightEditor: aceDiffEditor;
    
    additions = ko.observable<number>(0);
    deletions = ko.observable<number>(0);
    
    identicalContent: KnockoutComputed<boolean>;
    leftRevisionIsNewer = ko.observable<boolean>();
    
    leftGutterClass: KnockoutComputed<string>;
    rightGutterClass: KnockoutComputed<string>;
    leftMarkerClass: KnockoutComputed<string>;
    rightMarkerClass: KnockoutComputed<string>;
    
    constructor(leftEditor: AceAjax.Editor, rightEditor: AceAjax.Editor, leftRevisionIsNewer: boolean) {
        this.leftRevisionIsNewer(leftRevisionIsNewer);

        this.initObservables();

        this.leftEditor = new aceDiffEditor(leftEditor, "left", this.leftGutterClass(), this.leftMarkerClass());
        this.rightEditor = new aceDiffEditor(rightEditor, "right", this.rightGutterClass(), this.rightMarkerClass());
        
        this.init();
    }
    
    private initObservables() {
        this.leftGutterClass = ko.pureComputed(() => {
            return this.leftRevisionIsNewer() ? "ace_added" : "ace_removed";
        })

        this.rightGutterClass = ko.pureComputed(() => {
            return this.leftRevisionIsNewer() ? "ace_removed" : "ace_added";
        })

        this.leftMarkerClass = ko.pureComputed(() => {
            return this.leftRevisionIsNewer() ? "ace_code-added" : "ace_code-removed";
        })

        this.rightMarkerClass = ko.pureComputed(() => {
            return this.leftRevisionIsNewer() ? "ace_code-removed" : "ace_code-added";
        })
        
        this.identicalContent = ko.pureComputed(() => {
            const a = this.additions();
            const d = this.deletions();
            return a === 0 && d === 0;
        });
    }
    
    private init() {
        this.computeDifference();
        
        this.leftEditor.synchronizeScroll(this.rightEditor);
        this.rightEditor.synchronizeScroll(this.leftEditor);

        this.leftEditor.synchronizeFolds(this.rightEditor);
        this.rightEditor.synchronizeFolds(this.leftEditor);

        //initial sync:
        this.rightEditor.getSession().setScrollTop(this.leftEditor.getSession().getScrollTop());
    }

    //
    // trying to convert this logic from ace-diff
    //
    
    // private getSingleDiffInfo(editor:  aceDiffEditor, offset: number, diffString: string) {
    //     const info = {
    //         startLine: 0,
    //         startChar: 0,
    //         endLine: 0,
    //         endChar: 0,
    //     };
    //     const endCharNum = offset + diffString.length;
    //     let runningTotal = 0;
    //     let startLineSet = false;
    //     let endLineSet = false;
    //
    //     editor.getAllLines().forEach((lineLength, lineIndex) => { 
    //         runningTotal += lineLength;
    //
    //         if (!startLineSet && offset < runningTotal) {
    //             info.startLine = lineIndex;
    //             info.startChar = offset - runningTotal + lineLength;
    //             startLineSet = true;
    //         }
    //
    //         if (!endLineSet && endCharNum <= runningTotal) {
    //             info.endLine = lineIndex;
    //             info.endChar = endCharNum - runningTotal + lineLength;
    //             endLineSet = true;
    //         }
    //     });
    //
    //     // if the start char is the final char on the line, it's a newline & we ignore it
    //     if (info.startChar > 0 && getCharsOnLine(editor, info.startLine) === info.startChar) {
    //         info.startLine++;
    //         info.startChar = 0;
    //     }
    //
    //     // if the end char is the first char on the line, we don't want to highlight that extra line
    //     if (info.endChar === 0) {
    //         info.endLine--;
    //     }
    //
    //     const endsWithNewline = /\n$/.test(diffString);
    //     if (info.startChar > 0 && endsWithNewline) {
    //         info.endLine++;
    //     }
    //
    //     return info;
    // }


    //
    // trying to convert this logic from ace-diff
    //
    
    // private computeDiff(diffType: number, offsetLeft: number, offsetRight: number, diffText: string) {
    //     let lineInfo = {};
    //
    //     // this was added in to hack around an oddity with the Google lib. Sometimes it would include a newline
    //     // as the first char for a diff, other times not - and it would change when you were typing on-the-fly. This
    //     // is used to level things out so the diffs don't appear to shift around
    //     let newContentStartsWithNewline = /^\n/.test(diffText);
    //
    //     if (diffType === DIFF_INSERT) {
    //         // pretty confident this returns the right stuff for the left editor: start & end line & char
    //         var info = getSingleDiffInfo(this.leftEditor, offsetLeft, diffText);
    //
    //         // this is the ACTUAL undoctored current line in the other editor. It's always right. Doesn't mean it's
    //         // going to be used as the start line for the diff though.
    //         var currentLineOtherEditor = getLineForCharPosition(this.rightEditor, offsetRight);
    //         var numCharsOnLineOtherEditor = getCharsOnLine(this.rightEditor, currentLineOtherEditor);
    //         const numCharsOnLeftEditorStartLine = getCharsOnLine(this.leftEditor, info.startLine);
    //         var numCharsOnLine = getCharsOnLine(this.leftEditor, info.startLine);
    //
    //         // this is necessary because if a new diff starts on the FIRST char of the left editor, the diff can comes
    //         // back from google as being on the last char of the previous line so we need to bump it up one
    //         let rightStartLine = currentLineOtherEditor;
    //         if (numCharsOnLine === 0 && newContentStartsWithNewline) {
    //             newContentStartsWithNewline = false;
    //         }
    //         if (info.startChar === 0 && isLastChar(this.rightEditor, offsetRight, newContentStartsWithNewline)) {
    //             rightStartLine = currentLineOtherEditor + 1;
    //         }
    //
    //         var sameLineInsert = info.startLine === info.endLine;
    //
    //         // whether or not this diff is a plain INSERT into the other editor, or overwrites a line take a little work to
    //         // figure out. This feels like the hardest part of the entire script.
    //         var numRows = 0;
    //         if (
    //
    //             // dense, but this accommodates two scenarios:
    //             // 1. where a completely fresh new line is being inserted in left editor, we want the line on right to stay a 1px line
    //             // 2. where a new character is inserted at the start of a newline on the left but the line contains other stuff,
    //             //    we DO want to make it a full line
    //             (info.startChar > 0 || (sameLineInsert && diffText.length < numCharsOnLeftEditorStartLine))
    //
    //             // if the right editor line was empty, it's ALWAYS a single line insert [not an OR above?]
    //             && numCharsOnLineOtherEditor > 0
    //
    //             // if the text being inserted starts mid-line
    //             && (info.startChar < numCharsOnLeftEditorStartLine)) {
    //             numRows++;
    //         }
    //
    //         lineInfo = {
    //             leftStartLine: info.startLine,
    //             leftEndLine: info.endLine + 1,
    //             rightStartLine,
    //             rightEndLine: rightStartLine + numRows,
    //         };
    //     } else {
    //         var info = getSingleDiffInfo(acediff.editors.right, offsetRight, diffText);
    //
    //         var currentLineOtherEditor = getLineForCharPosition(this.leftEditor, offsetLeft);
    //         var numCharsOnLineOtherEditor = getCharsOnLine(this.leftEditor, currentLineOtherEditor);
    //         const numCharsOnRightEditorStartLine = getCharsOnLine(this.rightEditor, info.startLine);
    //         var numCharsOnLine = getCharsOnLine(this.rightEditor, info.startLine);
    //
    //         // this is necessary because if a new diff starts on the FIRST char of the left editor, the diff can comes
    //         // back from google as being on the last char of the previous line so we need to bump it up one
    //         let leftStartLine = currentLineOtherEditor;
    //         if (numCharsOnLine === 0 && newContentStartsWithNewline) {
    //             newContentStartsWithNewline = false;
    //         }
    //         if (info.startChar === 0 && isLastChar(this.leftEditor, offsetLeft, newContentStartsWithNewline)) {
    //             leftStartLine = currentLineOtherEditor + 1;
    //         }
    //
    //         var sameLineInsert = info.startLine === info.endLine;
    //         var numRows = 0;
    //         if (
    //
    //             // dense, but this accommodates two scenarios:
    //             // 1. where a completely fresh new line is being inserted in left editor, we want the line on right to stay a 1px line
    //             // 2. where a new character is inserted at the start of a newline on the left but the line contains other stuff,
    //             //    we DO want to make it a full line
    //             (info.startChar > 0 || (sameLineInsert && diffText.length < numCharsOnRightEditorStartLine))
    //
    //             // if the right editor line was empty, it's ALWAYS a single line insert [not an OR above?]
    //             && numCharsOnLineOtherEditor > 0
    //
    //             // if the text being inserted starts mid-line
    //             && (info.startChar < numCharsOnRightEditorStartLine)) {
    //             numRows++;
    //         }
    //
    //         lineInfo = {
    //             leftStartLine,
    //             leftEndLine: leftStartLine + numRows,
    //             rightStartLine: info.startLine,
    //             rightEndLine: info.endLine + 1,
    //         };
    //     }
    //
    //     return lineInfo;
    // }

    //
    // trying to convert this logic from ace-diff
    //
    
    // private followAceDiffLogic(left: string, right: string) {
    //    
    //     const dmp = new diff_match_patch();
    //     const diff = dmp.diff_main(left, right);
    //     dmp.diff_cleanupSemantic(diff);
    //   
    //     const diffs = [];
    //     const offset = {
    //         left: 0,
    //         right: 0,
    //     };
    //
    //     diff.forEach((chunk, index, array) => {
    //         const chunkType = chunk[0];
    //         let text = chunk[1];
    //
    //         // Fix for #28 https://github.com/ace-diff/ace-diff/issues/28
    //         if (array[index + 1] && text.endsWith('\n') && array[index + 1][1].startsWith('\n')) {
    //             text += '\n';
    //             diff[index][1] = text;
    //             diff[index + 1][1] = diff[index + 1][1].replace(/^\n/, '');
    //         }
    //
    //         // oddly, occasionally the algorithm returns a diff with no changes made
    //         if (text.length === 0) {
    //             return;
    //         }
    //         if (chunkType === DIFF_EQUAL) {
    //             offset.left += text.length;
    //             offset.right += text.length;
    //         } else if (chunkType === DIFF_DELETE) { 
    //             diffs.push(this.computeDiff(DIFF_DELETE, offset.left, offset.right, text));
    //             offset.right += text.length;
    //         } else if (chunkType === DIFF_INSERT) {
    //             diffs.push(this.computeDiff(DIFF_INSERT, offset.left, offset.right, text));
    //             offset.left += text.length;
    //         }
    //     }, this);
    //
    //     // simplify our computed diffs; this groups together multiple diffs on subsequent lines
    //     this.diffs = simplifyDiffs(this, diffs);
    //
    //     // if we're dealing with too many diffs, fail silently
    //     if (this.diffs.length > this.options.maxDiffs) {
    //         return;
    //     }
    // }

    private computeDifference() {
        const leftLines = this.leftEditor.getAllLines();
        const rightLines = this.rightEditor.getAllLines();
        
        this.followAceDiffLogic(leftLines.join("\r\n"), rightLines.join("\r\n")); // todo 
        
        const patch = diff.structuredPatch("left", "right",
            leftLines.join("\r\n"), rightLines.join("\r\n"),
            null, null, {
                context: 0
            });

        const leftGaps = patch.hunks
            .filter(x => x.oldLines < x.newLines)
            .map(hunk => ({
                emptyLinesCount: hunk.newLines - hunk.oldLines,
                firstLine: hunk.oldStart + hunk.oldLines
            } as gapItem));

        const rightGaps = patch.hunks
            .filter(x => x.oldLines > x.newLines)
            .map(hunk => ({
                emptyLinesCount: hunk.oldLines - hunk.newLines,
                firstLine: hunk.newStart + hunk.newLines
            } as gapItem));
        
        this.leftEditor.update(patch, leftGaps);
        this.rightEditor.update(patch, rightGaps);

        if (this.leftRevisionIsNewer()) {
            this.additions(this.leftEditor.getHighlightsCount());
            this.deletions(this.rightEditor.getHighlightsCount());
        } else {
            this.additions(this.rightEditor.getHighlightsCount());
            this.deletions(this.leftEditor.getHighlightsCount());
        }
    }

    refresh(leftRevisionIsNewer: boolean) {
        this.leftRevisionIsNewer(leftRevisionIsNewer);
        
        this.leftEditor.refresh(this.leftGutterClass(), this.leftMarkerClass());
        this.rightEditor.refresh(this.rightGutterClass(), this.rightMarkerClass());
        
        this.init();
    }
    
    destroy() {
        this.leftEditor.destroy();
        this.rightEditor.destroy();
    }
}

export = aceDiff;
