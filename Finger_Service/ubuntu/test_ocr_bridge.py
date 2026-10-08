import json
import threading
import subprocess
import unittest
from urllib.error import HTTPError
from urllib.request import Request, urlopen
from unittest.mock import patch
from http.server import HTTPServer
from ocr_bridge import Handler, parse_tsv


class BridgeTests(unittest.TestCase):
    def test_thai_lines_and_coordinates(self):
        header = "level\tpage_num\tblock_num\tpar_num\tline_num\tword_num\tleft\ttop\twidth\theight\tconf\ttext\n"
        data = "5\t1\t1\t1\t1\t1\t10\t20\t20\t10\t90\tบันทึก\n5\t1\t1\t1\t1\t2\t35\t20\t10\t10\t90\tค่า\n5\t1\t2\t1\t1\t1\t80\t20\t20\t10\t90\tCancel\n"
        self.assertEqual(parse_tsv(header+data), [dict(text="บันทึก ค่า", x=27.5, y=25), dict(text="Cancel", x=90, y=25)])

    def test_authenticated_http_and_errors(self):
        server = HTTPServer(("127.0.0.1", 0), Handler)
        server.token = "a"*64
        thread = threading.Thread(target=server.serve_forever, daemon=True)
        thread.start()
        url = f"http://127.0.0.1:{server.server_port}/ocr"
        def request(data, token=server.token):
            return urlopen(Request(url, data=data, headers={"X-OCR-Token": token}), timeout=3)
        try:
            with patch("ocr_bridge.recognize", return_value=[dict(text="บันทึก", x=4, y=5)]) as recognize:
                with self.assertRaises(HTTPError) as error:
                    request(b"image", "wrong")
                self.assertEqual(error.exception.code, 401)
                error.exception.close()
                recognize.assert_not_called()
                with self.assertRaises(HTTPError) as error:
                    request(b"not png")
                self.assertEqual(error.exception.code, 400)
                error.exception.close()
                with request(b"\x89PNG\r\n\x1a\nplaceholder") as response:
                    self.assertEqual(json.load(response)[0]["text"], "บันทึก")
            with patch("ocr_bridge.recognize", side_effect=OSError):
                with self.assertRaises(HTTPError) as error:
                    request(b"\x89PNG\r\n\x1a\nplaceholder")
                self.assertEqual(error.exception.code, 503)
                error.exception.close()
            with patch("ocr_bridge.recognize", side_effect=subprocess.TimeoutExpired("tesseract", 20)):
                with self.assertRaises(HTTPError) as error:
                    request(b"\x89PNG\r\n\x1a\nplaceholder")
                self.assertEqual(error.exception.code, 504)
                error.exception.close()
        finally:
            server.shutdown()
            server.server_close()
            thread.join()


if __name__ == "__main__":
    unittest.main()
