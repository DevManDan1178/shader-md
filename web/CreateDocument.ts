export function createDocument(html: string): string {
    return `
        <!DOCTYPE html>
        <html>
            <head>
                <meta charset="UTF-8">

                <style>
                    html {
                        margin: 15px;
                        background: transparent;
                    }

                    body {
                        margin: 0;
                        padding: 40px;
                        background: transparent;
                        color: #c9d1d9;
                        font-family: Arial, sans-serif;
                        min-height: 100vh;
                        height: auto;
                    }

                    h1 {
                        color: #58a6ff;
                    }

                    h2 {
                        color: #79c0ff;
                    }

                    code {
                        background: #161b22;
                        padding: 2px 5px;
                        border-radius: 4px;
                    }

                    pre {
                        background: #161b22;
                        padding: 16px;
                        border-radius: 8px;
                    }

                    img {
                        max-width: 100%;
                    }
                </style>
            </head>

            <body>
                ${html}
            </body>
        </html>
    `;
}