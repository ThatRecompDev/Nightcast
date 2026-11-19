// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using Phantom.Relay.Stream.Channels;

namespace Phantom.Relay.Stream.Tests.Channels;

public sealed class TitleInfoTests
{
    [Fact]
    public void The_title_id_is_read_from_content_carried_as_json_text()
    {
        string message = """{"type":"Message","target":"/streaming/properties/titleinfo","id":"1","content":"{\"titleid\":\"1234ABCD\",\"name\":\"A game\"}"}""";

        Assert.Equal("1234ABCD", TitleInfo.Read(message));
    }

    [Fact]
    public void The_content_may_be_an_object_and_the_id_a_number()
    {
        string message = """{"type":"Message","target":"/streaming/properties/titleinfo","content":{"TitleId":305419896}}""";

        Assert.Equal("305419896", TitleInfo.Read(message));
    }

    [Fact]
    public void Another_target_is_not_a_title()
    {
        string message = """{"type":"Message","target":"/streaming/touchcontrols/showlayoutv2","content":"{\"titleId\":\"1234ABCD\"}"}""";

        Assert.Null(TitleInfo.Read(message));
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("""{"target":"/streaming/properties/titleinfo"}""")]
    [InlineData("""{"target":"/streaming/properties/titleinfo","content":"not json"}""")]
    [InlineData("""{"target":"/streaming/properties/titleinfo","content":"{\"titleId\":\"\"}"}""")]
    [InlineData("""{"target":"/streaming/properties/titleinfo","content":"{\"name\":\"A game\"}"}""")]
    [InlineData("""[1,2]""")]
    public void Anything_else_reads_as_no_title(string message)
    {
        Assert.Null(TitleInfo.Read(message));
    }
}
