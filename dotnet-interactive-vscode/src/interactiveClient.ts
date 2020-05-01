import { Observable } from 'rxjs';
import { EventEnvelope } from './events';
import { ClientAdapter } from './interfaces';

export class InteractiveClient {

    constructor(readonly clientAdapter: ClientAdapter) {
    }

    completion(language: string, code: string, line: number, character: number): Observable<EventEnvelope> {
        let position = 0;
        let currentLine = 0;
        let currentCharacter = 0;
        for (; position < code.length; position++) {
            if (currentLine === line && currentCharacter === character) {
                break;
            }

            switch (code[position]) {
                case '\n':
                    currentLine++;
                    currentCharacter = 0;
                    break;
                default:
                    currentCharacter++;
                    break;
            }
        }
        let command = {
            code: code,
            cursorPosition: position,
        };

        return this.clientAdapter.submitCommand('RequestCompletion', command, language);
    }

    hover(language: string, code: string, line: number, character: number): Observable<EventEnvelope> {
        let b = Buffer.from(code);
        let command = {
            documentIdentifier: 'data:text/plain;base64,' + b.toString('base64'),
            position: {
                line: line,
                character: character,
            }
        };
        return this.clientAdapter.submitCommand('RequestHoverText', command, language);
    }

    submitCode(language: string, code: string): Observable<EventEnvelope> {
        let command = {
            code: code,
            submissionType: 0,
        };
        return this.clientAdapter.submitCommand('SubmitCode', command, language);
    }
}
