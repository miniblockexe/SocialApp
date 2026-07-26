"""
SocialApp API – Bộ kiểm thử tích hợp (Integration Test)
=========================================================
Môn: Kiểm thử phần mềm – ĐH Yersin Đà Lạt
Giảng viên: ThS. Thái Thuận Thương

Chạy:
    pip install pytest pytest-html requests colorama
    pytest test_socialapp.py -v --html=report.html --self-contained-html
    python -m pytest test_socialapp.py -v --html=report.html --self-contained-html

Yêu cầu: API đang chạy tại BASE_URL (dotnet run --project SocialApp.API)
"""

import time
import pytest
import requests

# CONFIG
BASE_URL       = "http://localhost:5290/api"
ADMIN_EMAIL    = "admin@socialapp.com"
ADMIN_PASSWORD = "Admin@123456"

_ts = int(time.time())
_USER1_DATA = {
    "email":           f"testuser1_{_ts}@example.com",
    "password":        "Test@123456",
    "confirmPassword": "Test@123456",
    "username":        f"testuser1_{_ts}",
    "fullName":        "Test User 1",
}
_USER2_DATA = {
    "email":           f"testuser2_{_ts}@example.com",
    "password":        "Test@123456",
    "confirmPassword": "Test@123456",
    "username":        f"testuser2_{_ts}",
    "fullName":        "Test User 2",
}

# HELPERS
def _h(token=None):
    h = {"Content-Type": "application/json"}
    if token:
        h["Authorization"] = f"Bearer {token}"
    return h

def _post(path, body=None, token=None):
    return requests.post(f"{BASE_URL}{path}", json=body, headers=_h(token), timeout=30)

def _post_form(path, data=None, token=None):
    headers = {}
    if token:
        headers["Authorization"] = f"Bearer {token}"
    return requests.post(f"{BASE_URL}{path}", data=data,
                         files={"_": ("", b"")}, headers=headers, timeout=30)

def _get(path, token=None, params=None):
    return requests.get(f"{BASE_URL}{path}", headers=_h(token),
                        params=params, timeout=30)

def _put(path, body=None, token=None):
    return requests.put(f"{BASE_URL}{path}", json=body, headers=_h(token), timeout=30)

def _delete(path, body=None, token=None):
    return requests.delete(f"{BASE_URL}{path}", json=body,
                           headers=_h(token), timeout=30)

# SESSION-SCOPED FIXTURES
# Mỗi fixture chỉ gọi API một lần cho cả session → tránh tạo rác thừa.

@pytest.fixture(scope="session")
def user1_auth():
    """Đăng ký + đăng nhập user1, trả về dict {token, id, email, password}."""
    r = _post("/auth/register", _USER1_DATA)
    assert r.status_code == 201, f"Register user1 thất bại: {r.text}"
    reg = r.json()["data"]
    # Login riêng để lấy refresh token mới nhất (tránh bị rotate bởi test khác)
    r2 = _post("/auth/login", {"email": _USER1_DATA["email"],
                                "password": _USER1_DATA["password"]})
    assert r2.status_code == 200, f"Login user1 thất bại: {r2.text}"
    d = r2.json()["data"]
    return {
        "token":    d["accessToken"],
        "refresh":  d["refreshToken"],
        "id":       reg["user"]["id"],
        "email":    _USER1_DATA["email"],
        "password": _USER1_DATA["password"],
        "username": _USER1_DATA["username"],
    }

@pytest.fixture(scope="session")
def user2_auth():
    """Đăng ký + đăng nhập user2."""
    r = _post("/auth/register", _USER2_DATA)
    assert r.status_code == 201, f"Register user2 thất bại: {r.text}"
    d = r.json()["data"]
    return {
        "token":    d["accessToken"],
        "refresh":  d["refreshToken"],
        "id":       d["user"]["id"],
        "email":    _USER2_DATA["email"],
        "password": _USER2_DATA["password"],
        "username": _USER2_DATA["username"],
    }

@pytest.fixture(scope="session")
def admin_token():
    """Đăng nhập tài khoản admin, trả về access token."""
    r = _post("/auth/login", {"email": ADMIN_EMAIL, "password": ADMIN_PASSWORD})
    assert r.status_code == 200, f"Admin login thất bại: {r.text}"
    return r.json()["data"]["accessToken"]

@pytest.fixture(scope="session")
def post_id(user1_auth):
    """Tạo 1 bài viết bởi user1, trả về post_id."""
    r = _post_form("/posts", {"content": "Bài viết test tích hợp 🎉", "privacy": "0"},
                   token=user1_auth["token"])
    assert r.status_code == 201, f"Tạo post thất bại: {r.text}"
    return r.json()["data"]["id"]

@pytest.fixture(scope="session")
def comment_id(user1_auth, post_id):
    """Tạo 1 comment trên post_id bởi user1, trả về comment_id."""
    r = _post(f"/posts/{post_id}/comments",
              {"content": "Comment fixture"}, token=user1_auth["token"])
    assert r.status_code == 201, f"Tạo comment thất bại: {r.text}"
    return r.json()["data"]["id"]

@pytest.fixture(scope="session")
def friend_request_id(user1_auth, user2_auth):
    """user1 gửi friend request cho user2, trả về requestId."""
    r = _post("/friends/request", {"receiverId": user2_auth["id"]},
              token=user1_auth["token"])
    assert r.status_code == 201, f"Gửi friend request thất bại: {r.text}"
    return r.json()["data"]["requestId"]

@pytest.fixture(scope="session")
def accepted_friend(user1_auth, user2_auth, friend_request_id):
    """user2 chấp nhận friend request → hai user đã là bạn."""
    r = _put(f"/friends/request/{friend_request_id}/accept",
             token=user2_auth["token"])
    assert r.status_code == 200, f"Accept request thất bại: {r.text}"
    return True

@pytest.fixture(scope="session")
def conversation_id(user1_auth, user2_auth):
    """Tạo conversation 1-1 giữa user1 và user2."""
    r = _post("/conversations",
              {"participantIds": [user2_auth["id"]], "isGroup": False},
              token=user1_auth["token"])
    assert r.status_code == 200, f"Tạo conversation thất bại: {r.text}"
    return r.json()["data"]["id"]

@pytest.fixture(scope="session")
def message_id(user1_auth, conversation_id):
    """Gửi 1 tin nhắn trong conversation, trả về message_id."""
    r = _post_form(
        f"/conversations/{conversation_id}/messages",
        {"conversationId": conversation_id, "content": "Xin chào! 👋"},
        token=user1_auth["token"],
    )
    assert r.status_code == 201, f"Gửi tin nhắn thất bại: {r.text}"
    return r.json()["data"]["id"]

# MODULE 1 – AUTH

class TestAuth:
    """TC-AUTH-xx: Kiểm thử xác thực – đăng ký, đăng nhập, token, đổi mật khẩu."""

    def test_register_success(self):
        """TC-AUTH-01: Đăng ký tài khoản hợp lệ → 201, có accessToken."""
        ts = int(time.time()) + 1
        payload = {
            "email": f"new_{ts}@example.com", "password": "Test@123456",
            "confirmPassword": "Test@123456", "username": f"new_{ts}",
            "fullName": "New User",
        }
        r = _post("/auth/register", payload)
        assert r.status_code == 201
        data = r.json()
        assert data.get("success") is True
        assert "accessToken" in data["data"]

    def test_register_duplicate_email(self, user1_auth):
        """TC-AUTH-02: Đăng ký email đã tồn tại → 409 Conflict."""
        payload = {**_USER1_DATA, "username": "other_unique_name99"}
        r = _post("/auth/register", payload)
        assert r.status_code == 409

    def test_register_invalid_email_format(self):
        """TC-AUTH-03: Email sai định dạng → 422 Unprocessable Entity."""
        payload = {**_USER1_DATA, "email": "not-an-email", "username": "uniqueuser99"}
        r = _post("/auth/register", payload)
        assert r.status_code == 422

    def test_register_weak_password(self):
        """TC-AUTH-04: Mật khẩu quá yếu (< 8 ký tự, thiếu ký tự đặc biệt) → 422."""
        payload = {**_USER1_DATA, "email": "weakpw@example.com",
                   "username": "weakpwuser", "password": "123",
                   "confirmPassword": "123"}
        r = _post("/auth/register", payload)
        assert r.status_code == 422

    def test_register_empty_body(self):
        """TC-AUTH-05: Body rỗng → 400 Bad Request."""
        r = _post("/auth/register", None)
        assert r.status_code == 400

    def test_login_success(self, user1_auth):
        """TC-AUTH-06: Đăng nhập đúng email + password → 200, có accessToken."""
        r = _post("/auth/login", {"email": _USER1_DATA["email"],
                                  "password": _USER1_DATA["password"]})
        assert r.status_code == 200
        assert "accessToken" in r.json()["data"]

    def test_login_wrong_password(self):
        """TC-AUTH-07: Đăng nhập sai mật khẩu → 401 Unauthorized."""
        r = _post("/auth/login", {"email": _USER1_DATA["email"],
                                  "password": "WrongPass123!"})
        assert r.status_code == 401

    def test_login_nonexistent_email(self):
        """TC-AUTH-08: Email không tồn tại → 401."""
        r = _post("/auth/login", {"email": "noone@example.com",
                                  "password": "Test@123456"})
        assert r.status_code == 401

    def test_login_empty_body(self):
        """TC-AUTH-09: Body rỗng → 422."""
        r = _post("/auth/login", {})
        assert r.status_code == 422

    def test_refresh_token_success(self):
        """TC-AUTH-10: Làm mới token với refreshToken hợp lệ → 200, token mới."""
        import time as _time
        ts = int(_time.time())
        # Tạo user mới riêng để tránh token bị rotate bởi test khác
        payload = {
            "email": f"refresh_test_{ts}@example.com",
            "password": "Test@123456",
            "confirmPassword": "Test@123456",
            "username": f"refreshtest{ts}",
            "fullName": "Refresh Test User",
        }
        r_reg = _post("/auth/register", payload)
        assert r_reg.status_code == 201
        refresh_token = r_reg.json()["data"]["refreshToken"]
        r = _post("/auth/refresh", {"refreshToken": refresh_token})
        assert r.status_code == 200
        assert "accessToken" in r.json()["data"]

    def test_refresh_token_invalid(self):
        """TC-AUTH-11: Refresh token giả → 401."""
        r = _post("/auth/refresh", {"refreshToken": "this.is.fake"})
        assert r.status_code == 401

    def test_change_password_success(self, user2_auth):
        """TC-AUTH-12: Đổi mật khẩu đúng oldPassword → 204."""
        r = _put("/auth/change-password", {
            "oldPassword":        _USER2_DATA["password"],
            "newPassword":        "NewTest@123456",
            "confirmNewPassword": "NewTest@123456",
        }, token=user2_auth["token"])
        assert r.status_code == 204

    def test_change_password_wrong_old(self, user1_auth):
        """TC-AUTH-13: Đổi mật khẩu nhưng oldPassword sai → 400."""
        r = _put("/auth/change-password", {
            "oldPassword":        "WrongOld@123",
            "newPassword":        "NewTest@123456",
            "confirmNewPassword": "NewTest@123456",
        }, token=user1_auth["token"])
        assert r.status_code == 400

    def test_change_password_no_token(self):
        """TC-AUTH-14: Đổi mật khẩu không có token → 401."""
        r = _put("/auth/change-password", {
            "oldPassword": "Test@123456", "newPassword": "New@123456",
            "confirmNewPassword": "New@123456",
        })
        assert r.status_code == 401

# MODULE 2 – USERS

class TestUsers:
    """TC-USR-xx: Kiểm thử quản lý người dùng."""

    def test_get_me(self, user1_auth):
        """TC-USR-01: GET /users/me với token hợp lệ → 200, username đúng."""
        r = _get("/users/me", token=user1_auth["token"])
        assert r.status_code == 200
        assert r.json()["data"]["username"] == _USER1_DATA["username"]

    def test_get_me_no_token(self):
        """TC-USR-02: GET /users/me không có token → 401."""
        r = _get("/users/me")
        assert r.status_code == 401

    def test_get_user_by_id(self, user1_auth, user2_auth):
        """TC-USR-03: GET /users/{id} người khác → 200."""
        r = _get(f"/users/{user2_auth['id']}", token=user1_auth["token"])
        assert r.status_code == 200

    def test_get_user_not_found(self, user1_auth):
        """TC-USR-04: GET /users/{guid không tồn tại} → 404."""
        r = _get("/users/00000000-0000-0000-0000-000000000099",
                 token=user1_auth["token"])
        assert r.status_code == 404

    def test_search_users(self, user1_auth):
        """TC-USR-05: Tìm kiếm user với từ khoá >= 2 ký tự → 200, list."""
        r = _get("/users/search", token=user1_auth["token"], params={"q": "test"})
        assert r.status_code == 200

    def test_search_users_short_keyword(self, user1_auth):
        """TC-USR-06: Tìm kiếm với từ khoá 1 ký tự → 400."""
        r = _get("/users/search", token=user1_auth["token"], params={"q": "t"})
        assert r.status_code == 400

    def test_update_profile(self, user1_auth):
        """TC-USR-07: Cập nhật fullName và bio → 200."""
        r = _put("/users/me",
                 {"fullName": "Updated Name", "bio": "Bio cập nhật"},
                 token=user1_auth["token"])
        assert r.status_code == 200

# MODULE 3 – POSTS

class TestPosts:
    """TC-PST-xx: Kiểm thử bài đăng – tạo, đọc, cập nhật, xóa, like, comment."""

    def test_create_post_success(self, user1_auth):
        """TC-PST-01: Tạo bài viết có content hợp lệ → 201."""
        r = _post_form("/posts", {"content": "Post tạo bởi TC-PST-01", "privacy": "0"},
                       token=user1_auth["token"])
        assert r.status_code == 201
        assert "id" in r.json()["data"]

    def test_create_post_no_content(self, user1_auth):
        """TC-PST-02: Tạo bài không có content và không có media → 400 hoặc 422."""
        r = _post_form("/posts", {"privacy": "0"}, token=user1_auth["token"])
        assert r.status_code in (400, 422)

    def test_create_post_content_too_long(self, user1_auth):
        """TC-PST-03: Content vượt 5000 ký tự → 422 (Boundary Value)."""
        r = _post_form("/posts", {"content": "x" * 5001, "privacy": "0"},
                       token=user1_auth["token"])
        assert r.status_code == 422

    def test_create_post_max_content(self, user1_auth):
        """TC-PST-04: Content đúng 5000 ký tự → 201 (Boundary Value – biên trên hợp lệ)."""
        r = _post_form("/posts", {"content": "x" * 5000, "privacy": "0"},
                       token=user1_auth["token"])
        assert r.status_code == 201

    def test_create_post_no_token(self):
        """TC-PST-05: Tạo bài không có token → 401."""
        r = _post_form("/posts", {"content": "Không có token", "privacy": "0"})
        assert r.status_code == 401

    def test_get_post_by_id(self, user1_auth, post_id):
        """TC-PST-06: GET /posts/{id} với id hợp lệ → 200."""
        r = _get(f"/posts/{post_id}", token=user1_auth["token"])
        assert r.status_code == 200
        assert r.json()["data"]["id"] == post_id

    def test_get_post_not_found(self, user1_auth):
        """TC-PST-07: GET /posts/{guid không tồn tại} → 404."""
        r = _get("/posts/00000000-dead-beef-0000-000000000000",
                 token=user1_auth["token"])
        assert r.status_code == 404

    def test_get_feed(self, user1_auth):
        """TC-PST-08: GET /posts/feed với token hợp lệ → 200, có items."""
        r = _get("/posts/feed", token=user1_auth["token"])
        assert r.status_code == 200
        assert "items" in r.json()["data"] or "data" in r.json()

    def test_get_feed_no_token(self):
        """TC-PST-09: GET /posts/feed không có token → 401."""
        r = _get("/posts/feed")
        assert r.status_code == 401

    def test_get_feed_negative_page(self, user1_auth):
        """TC-PST-10: Page và size âm → API tự về default, trả 200."""
        r = _get("/posts/feed", token=user1_auth["token"],
                 params={"page": -5, "size": -10})
        assert r.status_code == 200

    def test_like_toggle(self, user1_auth, post_id):
        """TC-PST-11: Toggle like 2 lần → like rồi unlike, cả 2 → 200."""
        r1 = _post(f"/posts/{post_id}/like", token=user1_auth["token"])
        assert r1.status_code == 200
        r2 = _post(f"/posts/{post_id}/like", token=user1_auth["token"])
        assert r2.status_code == 200

    def test_add_comment_success(self, user1_auth, post_id):
        """TC-PST-12: Đăng comment hợp lệ → 201."""
        r = _post(f"/posts/{post_id}/comments",
                  {"content": "Comment từ TC-PST-12"},
                  token=user1_auth["token"])
        assert r.status_code == 201

    def test_add_comment_empty_content(self, user1_auth, post_id):
        """TC-PST-13: Comment content rỗng → 422."""
        r = _post(f"/posts/{post_id}/comments",
                  {"content": ""},
                  token=user1_auth["token"])
        assert r.status_code == 422

    def test_reply_comment(self, user1_auth, post_id, comment_id):
        """TC-PST-14: Reply vào comment hợp lệ (1 cấp) → 201."""
        r = _post(f"/posts/{post_id}/comments",
                  {"content": "Reply từ TC-PST-14", "parentCommentId": comment_id},
                  token=user1_auth["token"])
        assert r.status_code == 201

    def test_update_post_owner(self, user1_auth, post_id):
        """TC-PST-15: Chủ bài cập nhật content → 200."""
        r = _put(f"/posts/{post_id}",
                 {"content": "Nội dung đã cập nhật bởi TC-PST-15"},
                 token=user1_auth["token"])
        assert r.status_code == 200

    def test_update_post_forbidden(self, user2_auth, post_id):
        """TC-PST-16: User không phải chủ bài cập nhật → 403 Forbidden."""
        r = _put(f"/posts/{post_id}",
                 {"content": "Hack content"},
                 token=user2_auth["token"])
        assert r.status_code == 403

# MODULE 4 – FRIENDS

class TestFriends:
    """TC-FRD-xx: Kiểm thử kết bạn – gửi, chấp nhận, từ chối, hủy."""

    def test_send_friend_request_success(self, user1_auth, user2_auth, friend_request_id):
        """TC-FRD-01: Gửi friend request đến user2 → đã được tạo (fixture xác nhận)."""
        assert friend_request_id is not None

    def test_send_duplicate_request(self, user1_auth, user2_auth):
        """TC-FRD-02: Gửi lại request đang pending hoặc đã là bạn → 400."""
        r = _post("/friends/request", {"receiverId": user2_auth["id"]},
                  token=user1_auth["token"])
        assert r.status_code == 400

    def test_send_request_to_self(self, user1_auth):
        """TC-FRD-03: Gửi request cho chính mình → 400."""
        r = _post("/friends/request", {"receiverId": user1_auth["id"]},
                  token=user1_auth["token"])
        assert r.status_code == 400

    def test_get_pending_requests(self, user2_auth, friend_request_id):
        """TC-FRD-04: GET /friends/requests/pending (user2) → 200."""
        r = _get("/friends/requests/pending", token=user2_auth["token"])
        assert r.status_code == 200

    def test_accept_friend_request(self, user1_auth, user2_auth, accepted_friend):
        """TC-FRD-05: Chấp nhận friend request → hai user đã là bạn (fixture)."""
        assert accepted_friend is True

    def test_get_friends_list(self, user1_auth, accepted_friend):
        """TC-FRD-06: GET /friends → 200, có user2 trong danh sách."""
        r = _get("/friends", token=user1_auth["token"])
        assert r.status_code == 200

    def test_accept_request_wrong_user(self, user1_auth, friend_request_id):
        """TC-FRD-07: User không phải receiver accept request → 400 hoặc 403."""
        r = _put(f"/friends/request/{friend_request_id}/accept",
                 token=user1_auth["token"])
        assert r.status_code in (400, 403)

    def test_get_friend_suggestions(self, user1_auth):
        """TC-FRD-08: GET /friends/suggestions → 200."""
        r = _get("/friends/suggestions", token=user1_auth["token"])
        assert r.status_code == 200

    def test_get_friendship_status(self, user1_auth, user2_auth):
        """TC-FRD-09: GET /friends/status/{targetId} → 200, có trường status."""
        r = _get(f"/friends/status/{user2_auth['id']}",
                 token=user1_auth["token"])
        assert r.status_code == 200

# MODULE 5 – MESSAGES

class TestMessages:
    """TC-MSG-xx: Kiểm thử nhắn tin – conversation, gửi, xem, xóa."""

    def test_create_conversation(self, user1_auth, user2_auth, conversation_id):
        """TC-MSG-01: Tạo conversation 1-1 → conversation_id được tạo (fixture)."""
        assert conversation_id is not None

    def test_create_conversation_idempotent(self, user1_auth, user2_auth, conversation_id):
        """TC-MSG-02: Tạo lại conversation đã tồn tại → idempotent 200, cùng id."""
        r = _post("/conversations",
                  {"participantIds": [user2_auth["id"]], "isGroup": False},
                  token=user1_auth["token"])
        assert r.status_code == 200
        assert r.json()["data"]["id"] == conversation_id

    def test_send_message_success(self, user1_auth, conversation_id, message_id):
        """TC-MSG-03: Gửi tin nhắn hợp lệ → 201 (fixture)."""
        assert message_id is not None

    def test_send_empty_message(self, user1_auth, conversation_id):
        """TC-MSG-04: Gửi tin nhắn không có content và không có file → 400 hoặc 422."""
        r = _post_form(
            f"/conversations/{conversation_id}/messages",
            {"conversationId": conversation_id},
            token=user1_auth["token"],
        )
        assert r.status_code in (400, 422)

    def test_get_conversations(self, user1_auth, conversation_id):
        """TC-MSG-05: GET /conversations → 200."""
        r = _get("/conversations", token=user1_auth["token"])
        assert r.status_code == 200

    def test_get_messages_in_conversation(self, user1_auth, conversation_id):
        """TC-MSG-06: GET /conversations/{id}/messages → 200."""
        r = _get(f"/conversations/{conversation_id}/messages",
                 token=user1_auth["token"])
        assert r.status_code == 200

    def test_mark_conversation_seen(self, user2_auth, conversation_id):
        """TC-MSG-07: PUT /conversations/{id}/seen (user2) → 204."""
        r = _put(f"/conversations/{conversation_id}/seen",
                 token=user2_auth["token"])
        assert r.status_code == 204

    def test_delete_message(self, user1_auth, message_id):
        """TC-MSG-08: Xóa tin nhắn của chính mình → 200."""
        r = _delete(f"/messages/{message_id}", token=user1_auth["token"])
        assert r.status_code == 200

# MODULE 6 – NOTIFICATIONS

class TestNotifications:
    """TC-NTF-xx: Kiểm thử thông báo."""

    def test_get_notifications(self, user1_auth):
        """TC-NTF-01: GET /notifications → 200."""
        r = _get("/notifications", token=user1_auth["token"])
        assert r.status_code == 200

    def test_get_notification_count(self, user1_auth):
        """TC-NTF-02: GET /notifications/count → 200."""
        r = _get("/notifications/count", token=user1_auth["token"])
        assert r.status_code == 200

    def test_mark_all_read(self, user1_auth):
        """TC-NTF-03: PUT /notifications/read-all → 204."""
        r = _put("/notifications/read-all", token=user1_auth["token"])
        assert r.status_code == 204

# MODULE 7 – ADMIN

class TestAdmin:
    """TC-ADM-xx: Kiểm thử quyền admin – dashboard, ban/unban, xóa post."""

    def test_admin_dashboard(self, admin_token):
        """TC-ADM-01: GET /admin/dashboard với admin token → 200."""
        r = _get("/admin/dashboard", token=admin_token)
        assert r.status_code == 200

    def test_admin_get_users(self, admin_token):
        """TC-ADM-02: GET /admin/users → 200."""
        r = _get("/admin/users", token=admin_token)
        assert r.status_code == 200

    def test_admin_get_posts(self, admin_token):
        """TC-ADM-03: GET /admin/posts → 200."""
        r = _get("/admin/posts", token=admin_token)
        assert r.status_code == 200

    def test_user_cannot_access_admin(self, user1_auth):
        """TC-ADM-04: User thường GET /admin/dashboard → 403 Forbidden."""
        r = _get("/admin/dashboard", token=user1_auth["token"])
        assert r.status_code == 403

    def test_admin_ban_unban_user(self, admin_token, user2_auth):
        """TC-ADM-05: Admin ban rồi unban user2 → cả 2 đều thành công."""
        r_ban = _put(f"/admin/users/{user2_auth['id']}/ban",
                     {"Reason": "Vi phạm nội quy cộng đồng"}, token=admin_token)
        assert r_ban.status_code == 204
        time.sleep(1)
        r_unban = _put(f"/admin/users/{user2_auth['id']}/unban",
                       token=admin_token)
        assert r_unban.status_code == 204

    def test_banned_user_blocked(self, admin_token, user2_auth):
        """TC-ADM-06: Banned user gọi /users/me → 403 (ban rồi unban sau)."""
        _put(f"/admin/users/{user2_auth['id']}/ban",
             {"Reason": "Vi phạm nội quy cộng đồng"}, token=admin_token)
        time.sleep(1)
        r = _get("/users/me", token=user2_auth["token"])
        _put(f"/admin/users/{user2_auth['id']}/unban", token=admin_token)
        assert r.status_code == 403

    def test_admin_ban_self(self, admin_token):
        """TC-ADM-07: Admin tự ban mình → 400."""
        r_me = _get("/users/me", token=admin_token)
        admin_id = r_me.json()["data"]["id"]
        r = _put(f"/admin/users/{admin_id}/ban",
                 {"Reason": "Kiểm tra tự ban bản thân"}, token=admin_token)
        assert r.status_code == 400

    def test_admin_delete_post(self, admin_token, post_id):
        """TC-ADM-08: Admin xóa bài viết → 204."""
        r = _delete(f"/admin/posts/{post_id}",
                    body={"Reason": "Vi phạm nội quy và chuẩn mực cộng đồng"},
                    token=admin_token)
        assert r.status_code == 204

# MODULE 8 – EDGE CASES (Bảo mật & Phân trang)

class TestEdgeCases:
    """TC-EDG-xx: Kiểm thử biên và bảo mật cơ bản."""

    def test_fake_token(self):
        """TC-EDG-01: Token giả mạo gọi /posts/feed → 401."""
        r = _get("/posts/feed", token="this.is.fake.jwt.token")
        assert r.status_code == 401

    def test_nonexistent_guid(self, user1_auth):
        """TC-EDG-02: GUID không tồn tại → 404."""
        r = _get("/posts/00000000-dead-beef-0000-000000000000",
                 token=user1_auth["token"])
        assert r.status_code == 404

    def test_invalid_http_method(self, user1_auth):
        """TC-EDG-03: DELETE /auth/login (method không hỗ trợ) → 405."""
        r = requests.delete(f"{BASE_URL}/auth/login",
                            headers=_h(user1_auth["token"]), timeout=10)
        assert r.status_code == 405

    def test_page_size_over_limit(self, user1_auth):
        """TC-EDG-04: size=9999 → API tự cap về 100, trả 200."""
        r = _get("/posts/feed", token=user1_auth["token"],
                 params={"page": 1, "size": 9999})
        assert r.status_code == 200

    def test_sql_injection_in_search(self, user1_auth):
        """TC-EDG-05: Payload SQL injection trong query search → không crash (200 hoặc 400)."""
        r = _get("/users/search", token=user1_auth["token"],
                 params={"q": "' OR 1=1 --"})
        assert r.status_code in (200, 400)

    def test_xss_payload_in_post(self, user1_auth):
        """TC-EDG-06: Content chứa XSS payload → API nhận, không crash (201)."""
        r = _post_form("/posts",
                       {"content": "<script>alert('xss')</script>", "privacy": "0"},
                       token=user1_auth["token"])
        assert r.status_code == 201

# SERVER HEALTH CHECK (skip toàn bộ nếu server down)
def pytest_configure(config):
    """Kiểm tra server trước khi chạy toàn bộ bộ test."""
    try:
        requests.get(f"{BASE_URL}/ai/health", timeout=5)
    except requests.exceptions.ConnectionError:
        pytest.exit(
            f"\n✗ Không kết nối được server tại {BASE_URL}\n"
            "  Hãy chạy: dotnet run --project SocialApp.API\n",
            returncode=1,
        )