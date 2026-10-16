import http.server, sys, os
class H(http.server.SimpleHTTPRequestHandler):
    def end_headers(self):
        p=self.path.split('?')[0]
        if p.endswith('.gz'):
            self.send_header('Content-Encoding','gzip')
        http.server.SimpleHTTPRequestHandler.end_headers(self)
    def guess_type(self,path):
        p=path[:-3] if path.endswith('.gz') else path
        if p.endswith('.wasm'): return 'application/wasm'
        if p.endswith('.js'): return 'application/javascript'
        if p.endswith('.data'): return 'application/octet-stream'
        return super().guess_type(path)
    def log_message(self,*a): pass
os.chdir(sys.argv[1]); http.server.ThreadingHTTPServer(('127.0.0.1',int(sys.argv[2])),H).serve_forever()
