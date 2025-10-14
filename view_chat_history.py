import sqlite3
import msgpack
import json
from datetime import datetime

def ticks_to_datetime(ticks):
    # .NET ticks to datetime
    epoch_ticks = 621355968000000000  # 1970-01-01 in .NET ticks
    seconds = (ticks - epoch_ticks) / 10000000
    return datetime.fromtimestamp(seconds)

def view_chat_history(db_path):
    try:
        conn = sqlite3.connect(db_path)
    except Exception as e:
        print(f"Error connecting to database: {e}")
        return

    cursor = conn.cursor()

    # Get all chat IDs
    cursor.execute("SELECT Id, Topic FROM Chats WHERE IsDeleted = 0 ORDER BY CreatedAt DESC")
    chats = cursor.fetchall()

    print("Available Chat IDs:")
    print("=" * 50)
    for chat_id, topic in chats:
        print(f"ID: {chat_id}")
        print(f"Topic: {topic or 'No Topic'}")
        print("-" * 30)

    # Input ID
    chat_id = input("\nEnter Chat ID to view: ").strip()
    if not chat_id:
        print("No ID entered.")
        conn.close()
        return

    # Check if ID exists
    cursor.execute("SELECT COUNT(*) FROM Chats WHERE Id = ? AND IsDeleted = 0", (chat_id,))
    if cursor.fetchone()[0] == 0:
        print("Invalid Chat ID.")
        conn.close()
        return

    # Get nodes
    cursor.execute("SELECT Id, Payload, Author, CreatedAt FROM Nodes WHERE ChatContextId = ? ORDER BY CreatedAt", (chat_id,))
    nodes = cursor.fetchall()

    print(f"\nChat History for ID: {chat_id}")
    print("=" * 50)

    for node_id, payload, author, created_at in nodes:
        print(f"\n--- Node {node_id} ---")
        print(f"Author: {author}")
        print(f"Time: {ticks_to_datetime(created_at)}")
        print("-" * 30)

        try:
            data = msgpack.unpackb(payload, strict_map_key=False)
            # data is [tag, obj] for union
            if isinstance(data, list) and len(data) == 2:
                tag, obj = data
                if tag == 0:  # SystemChatMessage
                    if isinstance(obj, list) and len(obj) > 0:
                        print(f"System: {obj[0]}")
                    elif isinstance(obj, str):
                        print(f"System: {obj}")
                    else:
                        print(f"System: {obj.get(0, '')}")
                elif tag == 1:  # AssistantChatMessage
                    if isinstance(obj, dict):
                        spans = obj.get(5, [])
                        for span in spans:
                            content = span.get(0, '')
                            reasoning = span.get(4, '')
                            function_calls = span.get(1, [])
                            if content:
                                print(f"发言: {content}")
                            if reasoning:
                                print(f"推理: {reasoning}")
                            for fc in function_calls:
                                # fc is [tag, obj] for FunctionCallChatMessage
                                if isinstance(fc, list) and len(fc) == 2 and fc[0] == 4:
                                    fc_obj = fc[1]
                                    calls = fc_obj.get(6, [])
                                    results = fc_obj.get(7, [])
                                    for call in calls:
                                        name = call.get(0, '')
                                        args = call.get(1, {})
                                        print(f"工具调用: {name}")
                                        print(f"参数: {json.dumps(args, indent=2, ensure_ascii=False)}")
                                    for result in results:
                                        res = result.get(0, '')
                                        print(f"工具返回: {res}")
                    elif isinstance(obj, list) and len(obj) >= 6:
                        # obj = [None, None, created, finished, None, spans, ...]
                        spans = obj[5] if len(obj) > 5 else []
                        for span in spans:
                            if isinstance(span, list) and len(span) >= 5:
                                content = span[0] or ''
                                reasoning = span[4] or ''
                                function_calls = span[1] or []
                                if content:
                                    print(f"发言: {content}")
                                if reasoning:
                                    print(f"推理: {reasoning}")
                                for fc in function_calls:
                                    if isinstance(fc, list) and len(fc) == 2 and fc[0] == 4:
                                        fc_obj = fc[1]
                                        calls = fc_obj.get(6, [])
                                        results = fc_obj.get(7, [])
                                        for call in calls:
                                            name = call.get(0, '')
                                            args = call.get(1, {})
                                            print(f"工具调用: {name}")
                                            print(f"参数: {json.dumps(args, indent=2, ensure_ascii=False)}")
                                        for result in results:
                                            res = result.get(0, '')
                                            print(f"工具返回: {res}")
                    else:
                        print(f"Assistant: {obj}")
                elif tag == 2:  # UserChatMessage
                    if isinstance(obj, list) and len(obj) > 0:
                        print(f"用户: {obj[0]}")
                    elif isinstance(obj, str):
                        print(f"用户: {obj}")
                    else:
                        prompt = obj.get(0, '')
                        print(f"用户: {prompt}")
                elif tag == 3:  # ActionChatMessage
                    if isinstance(obj, dict):
                        content = obj.get(2, '')
                        print(f"Action: {content}")
                    else:
                        print(f"Action: {obj}")
                elif tag == 4:  # FunctionCallChatMessage
                    if isinstance(obj, dict):
                        calls = obj.get(6, [])
                        results = obj.get(7, [])
                        for call in calls:
                            name = call.get(0, '')
                            args = call.get(1, {})
                            print(f"工具调用: {name}")
                            print(f"参数: {json.dumps(args, indent=2, ensure_ascii=False)}")
                        for result in results:
                            res = result.get(0, '')
                            print(f"工具返回: {res}")
                    else:
                        print(f"FunctionCall: {obj}")
            else:
                print(f"Unknown format: {data}")
        except Exception as e:
            if 'extra data' in str(e):
                unpacker = msgpack.Unpacker()
                unpacker.feed(payload)
                for data in unpacker:
                    # process each data
                    if isinstance(data, list) and len(data) == 2:
                        tag, obj = data
                        if tag == 0:  # SystemChatMessage
                            if isinstance(obj, list) and len(obj) > 0:
                                print(f"System: {obj[0]}")
                            elif isinstance(obj, str):
                                print(f"System: {obj}")
                            else:
                                print(f"System: {obj.get(0, '')}")
                        elif tag == 1:  # AssistantChatMessage
                            if isinstance(obj, list) and len(obj) >= 6:
                                spans = obj[5] if len(obj) > 5 else []
                                for span in spans:
                                    if isinstance(span, list) and len(span) >= 5:
                                        content = span[0] or ''
                                        reasoning = span[4] or ''
                                        function_calls = span[1] or []
                                        if content:
                                            print(f"发言: {content}")
                                        if reasoning:
                                            print(f"推理: {reasoning}")
                                        for fc in function_calls:
                                            if isinstance(fc, list) and len(fc) == 2 and fc[0] == 4:
                                                fc_obj = fc[1]
                                                calls = fc_obj.get(6, [])
                                                results = fc_obj.get(7, [])
                                                for call in calls:
                                                    name = call.get(0, '')
                                                    args = call.get(1, {})
                                                    print(f"工具调用: {name}")
                                                    print(f"参数: {json.dumps(args, indent=2, ensure_ascii=False)}")
                                                for result in results:
                                                    res = result.get(0, '')
                                                    print(f"工具返回: {res}")
                            else:
                                print(f"Assistant: {obj}")
                        elif tag == 2:  # UserChatMessage
                            if isinstance(obj, list) and len(obj) > 0:
                                print(f"用户: {obj[0]}")
                            elif isinstance(obj, str):
                                print(f"用户: {obj}")
                            else:
                                prompt = obj.get(0, '')
                                print(f"用户: {prompt}")
                        elif tag == 3:  # ActionChatMessage
                            if isinstance(obj, dict):
                                content = obj.get(2, '')
                                print(f"Action: {content}")
                            else:
                                print(f"Action: {obj}")
                        elif tag == 4:  # FunctionCallChatMessage
                            if isinstance(obj, dict):
                                calls = obj.get(6, [])
                                results = obj.get(7, [])
                                for call in calls:
                                    name = call.get(0, '')
                                    args = call.get(1, {})
                                    print(f"工具调用: {name}")
                                    print(f"参数: {json.dumps(args, indent=2, ensure_ascii=False)}")
                                for result in results:
                                    res = result.get(0, '')
                                    print(f"工具返回: {res}")
                            else:
                                print(f"FunctionCall: {obj}")
                        else:
                            print(f"Unknown tag {tag}: {obj}")
                    else:
                        print(f"Unknown data: {data}")
            else:
                print(f"Error decoding payload: {e}")
                print(f"Raw payload length: {len(payload)} bytes")

    conn.close()

if __name__ == "__main__":
    db_path = r'C:\Users\Noto\AppData\Roaming\Everywhere\db\chat.db'
    view_chat_history(db_path)